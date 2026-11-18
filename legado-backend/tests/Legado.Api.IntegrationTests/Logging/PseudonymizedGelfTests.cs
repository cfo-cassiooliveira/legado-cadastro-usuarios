using Legado.Api.Common.Pseudonymization;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Pseudonimização na origem com o pipeline real da API (ADR-0003 do graylog): o que sai pelo sink GELF TCP nunca
/// leva o valor em claro, o texto já vai mascarado e cada campo pessoal ganha <c>*_token</c>. Sem chave, a API não sobe.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class PseudonymizedGelfTests(LegadoApiFactory factory)
{
    private const string Cpf = "123.456.789-09";
    private const string Phone = "+55 (11) 98765-4321";
    private const string UserName = " Maria.Silva ";
    private const string Email = "Ana.Souza@Gmail.com";

    // Valores de entrada e seus canônicos: nenhum pode aparecer em campo algum da mensagem GELF.
    private static readonly string[] ClearValues =
        ["123.456.789-09", "12345678909", "98765-4321", "11987654321", "Maria.Silva", "maria.silva", "Ana.Souza", "ana.souza"];

    private static readonly TimeSpan GelfTimeout = TimeSpan.FromSeconds(15);

    private const string ExceptionText = "Simulated failure without personal data";

    private static readonly Func<ILogger, string, string, IDisposable?> SignupScope =
        LoggerMessage.DefineScope<string, string>("Signup of {Email} with {Cpf}");

    [Fact]
    public async Task GelfMessage_WithPersonalDataAndException_CarriesOnlyMasksAndTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gelf = new GelfTcpCapture();
        await using var host = GraylogHost(gelf);
        var marker = Guid.NewGuid().ToString("N");
        var logger = host.Services.GetRequiredService<ILogger<PseudonymizedGelfTests>>();
        var tokens = host.Services.GetRequiredService<PseudonymTokenGenerator>();

        LogPersonalDataFailure(logger, new InvalidOperationException(ExceptionText), Cpf, Phone, UserName, Email, marker);

        var raw = await gelf.WaitForAsync(marker, GelfTimeout, ct);
        var fields = GelfTcpCapture.Fields(raw);
        foreach (var value in ClearValues)
        {
            raw.ShouldNotContain(value, Case.Insensitive, $"GELF leaked '{value}'");
        }

        fields["_Cpf"].ShouldBe("***.456.789-**");
        fields["_Phone"].ShouldBe("11*****4321");
        fields["_UserName"].ShouldBe("mar***");
        fields["_Email"].ShouldBe("ana***@gmail");
        fields["_Cpf_token"].ShouldBe(tokens.Generate(PersonalDataKind.Cpf, "12345678909"));
        fields["_Phone_token"].ShouldBe(tokens.Generate(PersonalDataKind.Phone, "11987654321"));
        fields["_UserName_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, "maria.silva"));
        fields["_Email_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, "ana.souza@gmail.com"));
        fields["_Email_token"].ShouldStartWith("k1-");
        // A exceção segue sem alteração (o Serilog não permite trocá-la): por isso ela nunca pode levar dado pessoal;
        // o que escapar é coberto pelo estágio 4 do pipeline do Graylog (ADR-0003 do graylog, item 8).
        fields.Where(f => f.Key.StartsWith("_Exception", StringComparison.Ordinal)).Select(f => f.Value)
            .ShouldContain(text => text.Contains(ExceptionText, StringComparison.Ordinal));

        // Texto livre (message/full_message): só a máscara, nunca o token.
        var message = fields["short_message"];
        message.ShouldContain("***.456.789-**");
        message.ShouldContain("11*****4321");
        message.ShouldContain("mar***");
        message.ShouldContain("ana***@gmail");
        foreach (var text in fields.Where(f => !f.Key.EndsWith(PersonalDataFields.TokenSuffix, StringComparison.Ordinal)))
        {
            text.Value.ShouldNotContain("k1-", Case.Sensitive, $"token leaked into {text.Key}");
        }
    }

    [Fact]
    public async Task GelfMessage_WithDestructuredObjectAndTemplateScope_LeaksNoClearValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gelf = new GelfTcpCapture();
        await using var host = GraylogHost(gelf);
        var marker = Guid.NewGuid().ToString("N");
        var logger = host.Services.GetRequiredService<ILogger<PseudonymizedGelfTests>>();
        var tokens = host.Services.GetRequiredService<PseudonymTokenGenerator>();
        var customer = new ProbeCustomer(Email, new ProbeAddress(Phone));

        // Escopo com template: o provider soma o campo Email e o texto renderizado em "Scope", ambos com o valor em claro.
        using (SignupScope(logger, Email, Cpf))
        using (Serilog.Context.LogContext.PushProperty("UserName", UserName))
        {
            LogCustomer(logger, customer, marker);
        }

        var raw = await gelf.WaitForAsync(marker, GelfTimeout, ct);
        var fields = GelfTcpCapture.Fields(raw);
        foreach (var value in ClearValues)
        {
            raw.ShouldNotContain(value, Case.Insensitive, $"GELF leaked '{value}'");
        }

        fields["_Customer.Email"].ShouldBe("ana***@gmail");
        fields["_Customer_Email_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, "ana.souza@gmail.com"));
        fields["_Customer.Address.Phone"].ShouldBe("11*****4321");
        fields["_Email_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, "ana.souza@gmail.com"));
        fields["_Cpf_token"].ShouldBe(tokens.Generate(PersonalDataKind.Cpf, "12345678909"));
        fields["_UserName"].ShouldBe("mar***");
        raw.ShouldContain("Signup of ana***@gmail with ***.456.789-**");
    }

    // Regra de docs/reference.md: o texto renderizado sai só com a máscara, nunca com o token, também com {@Obj}.
    [Fact]
    public async Task GelfMessage_WithDestructuredObject_RendersMaskWithoutTokenInText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gelf = new GelfTcpCapture();
        await using var host = GraylogHost(gelf);
        var marker = Guid.NewGuid().ToString("N");
        var logger = host.Services.GetRequiredService<ILogger<PseudonymizedGelfTests>>();

        var customer = new ProbeCustomer(Email, new ProbeAddress(Phone));

        LogCustomer(logger, customer, marker);

        var fields = GelfTcpCapture.Fields(await gelf.WaitForAsync(marker, GelfTimeout, ct));
        fields["short_message"].ShouldContain("ana***@gmail");
        foreach (var text in fields.Where(f => !f.Key.EndsWith(PersonalDataFields.TokenSuffix, StringComparison.Ordinal)))
        {
            text.Value.ShouldNotContain("k1-", Case.Sensitive, $"token leaked into {text.Key}");
        }
    }

    [Fact]
    public async Task GelfMessage_AfterRotationWithPreviousKeyRemoved_EmitsCurrentKeyToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var newKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var gelf = new GelfTcpCapture();
        // Como o docker-compose.yml depois da rotação: CURRENT_KEY_ID=k2, K2 preenchida e K1 vazia (apagada do .env).
        await using var host = GraylogHost(gelf, ("Logging:Pseudonymization:CurrentKeyId", "k2"),
            ("Logging:Pseudonymization:Keys:k2", newKey), ("Logging:Pseudonymization:Keys:k1", string.Empty));
        var marker = Guid.NewGuid().ToString("N");
        var logger = host.Services.GetRequiredService<ILogger<PseudonymizedGelfTests>>();

        LogPersonalDataFailure(logger, new InvalidOperationException(ExceptionText), Cpf, Phone, UserName, Email, marker);

        var fields = GelfTcpCapture.Fields(await gelf.WaitForAsync(marker, GelfTimeout, ct));
        var expected = new PseudonymTokenGenerator(Options.Create(new PseudonymizationOptions { CurrentKeyId = "k2", Keys = { ["k2"] = newKey } }))
            .Generate(PersonalDataKind.Cpf, "12345678909");
        fields["_Cpf_token"].ShouldBe(expected);
        fields["_Cpf_token"].ShouldStartWith("k2-");
    }

    [Theory]
    [InlineData("Logging:Pseudonymization:CurrentKeyId", "k9", "Keys:k9 is not configured")]
    [InlineData("Logging:Pseudonymization:Keys:k1", "c2hvcnQ=", "Keys:k1 must be Base64")]
    public void Startup_WithoutValidCurrentKey_FailsFast(string setting, string value, string expectedMessage)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var error = Should.Throw<OptionsValidationException>(() => host.CreateClient());

        error.Message.ShouldContain(expectedMessage);
        error.Message.ShouldNotContain(LegadoApiFactory.PseudonymizationKey);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Pseudonymization probe for {Cpf} {Phone} {UserName} {Email} marker {TestMarker}")]
    private static partial void LogPersonalDataFailure(ILogger logger, Exception exception, string cpf, string phone,
        string userName, string email, string testMarker);
    [LoggerMessage(Level = LogLevel.Information, Message = "Customer {@Customer} marker {TestMarker}")]
    private static partial void LogCustomer(ILogger logger, ProbeCustomer customer, string testMarker);

    private WebApplicationFactory<Program> GraylogHost(GelfTcpCapture gelf, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Graylog:Host", "127.0.0.1");
            builder.UseSetting("Graylog:Port", gelf.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    /// <summary>Objeto destruturado de teste (<c>{@Customer}</c>), com dado pessoal aninhado.</summary>
    public sealed record ProbeCustomer(string Email, ProbeAddress Address);

    /// <summary>Nível aninhado do objeto de teste.</summary>
    public sealed record ProbeAddress(string Phone);
}
