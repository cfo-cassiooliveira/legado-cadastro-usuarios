using Legado.Api.Common.Pseudonymization;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Critérios de aceite da US-01 e US-05 da spec 001 do graylog que os demais testes não cobriam no nível do enricher
/// e do GELF real: telefone fora do formato, correlação entre campos diferentes (UserName × Email), rotação de chave,
/// todos os nomes das listas <c>LGPD_*_FIELDS</c> e propriedades vindas do escopo de log (LogContext).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class PseudonymizationAcceptanceTests(LegadoApiFactory factory)
{
    // Chave dos vetores do plan.md §2 da spec 001 do graylog (bytes 00 01 02 … 1f): pública por definição.
    private const string VectorKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private const string CpfVectorToken = "k1-b81c27bb5f38080e6ca0ed38b828f262";
    private static readonly TimeSpan GelfTimeout = TimeSpan.FromSeconds(15);

    // Cópia literal das listas LGPD_*_FIELDS do ../graylog/provisioning/provision.sh: remover um nome do enricher falha aqui.
    public static TheoryData<string, string, string> AllFieldNames()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var name in new[] { "cpf", "Cpf", "CPF" })
        {
            data.Add(name, "123.456.789-09", "***.456.789-**");
        }

        foreach (var name in new[] { "cnpj", "Cnpj", "CNPJ" })
        {
            data.Add(name, "12.345.678/0001-90", "12.3**.***/***1-90");
        }

        foreach (var name in new[] { "phone", "Phone", "mobilePhone", "MobilePhone", "mobile_phone", "celular", "Celular", "telefone", "Telefone" })
        {
            data.Add(name, "+55 (11) 98765-4321", "11*****4321");
        }

        foreach (var name in new[] { "username", "user_name", "UserName", "Username", "login", "Login", "preferred_username", "email", "Email" })
        {
            data.Add(name, "Ana.Souza@Gmail.com", "ana***@gmail");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllFieldNames))]
    public void Enrich_EveryProvisionedFieldName_IsMaskedAndTokenized(string field, string raw, string mask)
    {
        var logEvent = Event(field, raw);

        Enricher("k1", ("k1", VectorKey)).Enrich(logEvent, new PropertyFactory());

        Scalar(logEvent, field).ShouldBe(mask);
        Scalar(logEvent, field + PersonalDataFields.TokenSuffix).ShouldNotBeNull().ShouldStartWith("k1-");
    }

    [Fact]
    public void Enrich_PhoneOutOfFormat_RedactsWithoutToken()
    {
        var logEvent = Event("Phone", "abc");

        Enricher("k1", ("k1", VectorKey)).Enrich(logEvent, new PropertyFactory());

        Scalar(logEvent, "Phone").ShouldBe(PersonalDataMasker.Redacted);
        logEvent.Properties.ContainsKey("Phone_token").ShouldBeFalse();
    }

    [Fact]
    public void Enrich_UserNameAndEmailOfSamePerson_ProduceSameToken()
    {
        var enricher = Enricher("k1", ("k1", VectorKey));
        var withUserName = Event("UserName", "Ana.Souza@Gmail.com");
        var withEmail = Event("Email", "ana.souza@gmail.com");

        enricher.Enrich(withUserName, new PropertyFactory());
        enricher.Enrich(withEmail, new PropertyFactory());

        Scalar(withUserName, "UserName_token").ShouldBe("k1-c362504bb56a3e35cc245073244c3da6");
        Scalar(withUserName, "UserName_token").ShouldBe(Scalar(withEmail, "Email_token"));
    }

    [Fact]
    public void Enrich_WithRotatedKey_EmitsTokenOfCurrentKeyOnly()
    {
        var otherKey = Convert.ToBase64String(Enumerable.Range(64, 32).Select(b => (byte)b).ToArray());
        var logEvent = Event("Cpf", "123.456.789-09");

        Enricher("k2", ("k1", VectorKey), ("k2", otherKey)).Enrich(logEvent, new PropertyFactory());

        var token = Scalar(logEvent, "Cpf_token").ShouldNotBeNull();
        token.ShouldStartWith("k2-");
        token.ShouldNotBe(CpfVectorToken);
        token[3..].ShouldNotBe(CpfVectorToken[3..]);
    }

    [Fact]
    public async Task GelfMessage_WithScopeAndOtherFieldNames_CarriesOnlyMasksAndTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gelf = new GelfTcpCapture();
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Graylog:Host", "127.0.0.1");
            builder.UseSetting("Graylog:Port", gelf.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
        var marker = Guid.NewGuid().ToString("N");
        var logger = host.Services.GetRequiredService<ILogger<PseudonymizationAcceptanceTests>>();
        var tokens = host.Services.GetRequiredService<PseudonymTokenGenerator>();

        // CPF chega pelo escopo (LogContext); CNPJ, celular e login pelo message template.
        using (logger.BeginScope(new Dictionary<string, object> { ["Cpf"] = "987.654.321-00" }))
        {
            LogProbe(logger, "98.765.432/0001-10", "(21) 99876-5432", "Joao.Pereira", marker);
        }

        var raw = await gelf.WaitForAsync(marker, GelfTimeout, ct);
        var fields = GelfTcpCapture.Fields(raw);
        foreach (var clear in new[] { "987.654.321-00", "98765432100", "98.765.432/0001-10", "98765432000110", "99876-5432", "21998765432", "Joao.Pereira", "joao.pereira" })
        {
            raw.ShouldNotContain(clear, Case.Insensitive, $"GELF leaked '{clear}'");
        }

        fields["_Cpf"].ShouldBe("***.654.321-**");
        fields["_Cpf_token"].ShouldBe(tokens.Generate(PersonalDataKind.Cpf, "98765432100"));
        fields["_Cnpj"].ShouldBe("98.7**.***/***1-10");
        fields["_Cnpj_token"].ShouldBe(tokens.Generate(PersonalDataKind.Cnpj, "98765432000110"));
        fields["_MobilePhone"].ShouldBe("21*****5432");
        fields["_MobilePhone_token"].ShouldBe(tokens.Generate(PersonalDataKind.Phone, "21998765432"));
        fields["_Login"].ShouldBe("joa***");
        fields["_Login_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, "joao.pereira"));
        fields["short_message"].ShouldContain("98.7**.***/***1-10");
    }

    [Fact]
    public void Startup_WithoutCurrentKeyIdConfigured_FailsFast()
    {
        using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Logging:Pseudonymization:CurrentKeyId", string.Empty));

        var error = Should.Throw<OptionsValidationException>(() => host.CreateClient());

        error.Message.ShouldContain("CurrentKeyId");
        error.Message.ShouldNotContain(LegadoApiFactory.PseudonymizationKey);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Acceptance probe {Cnpj} {MobilePhone} {Login} marker {TestMarker}")]
    private static partial void LogProbe(ILogger logger, string cnpj, string mobilePhone, string login, string testMarker);

    private static PseudonymizationEnricher Enricher(string current, params (string Id, string Key)[] keys)
    {
        var options = new PseudonymizationOptions { CurrentKeyId = current };
        foreach (var (id, key) in keys)
        {
            options.Keys[id] = key;
        }

        return new PseudonymizationEnricher(new PseudonymTokenGenerator(Options.Create(options)));
    }

    private static LogEvent Event(string name, string value) =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Information, null, new MessageTemplateParser().Parse("{" + name + "}"),
            [new LogEventProperty(name, new ScalarValue(value))]);

    private static string? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar ? scalar.Value?.ToString() : null;

    private sealed class PropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
