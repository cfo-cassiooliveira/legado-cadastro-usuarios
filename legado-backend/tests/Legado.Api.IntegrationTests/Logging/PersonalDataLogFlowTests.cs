using System.Net.Http.Json;
using System.Reflection;
using Legado.Api.Common.Pseudonymization;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Login e cadastro reais logam o dado pessoal como campo estruturado (<c>UserName</c>, <c>Email</c>, <c>MobilePhone</c>):
/// ele só sai do processo como máscara (ADR-0002 do graylog) + <c>*_token</c>, nunca em claro, e o cadastro rejeitado
/// não loga os valores enviados. A resposta ao cliente continua genérica.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PersonalDataLogFlowTests(LegadoApiFactory factory)
{
    private static readonly TimeSpan GelfTimeout = TimeSpan.FromSeconds(15);
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    [Fact]
    public async Task Register_WhenSucceeds_LogsMaskedUserNameEmailAndPhoneWithTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        var userName = $"maria.{Guid.NewGuid():N}"[..14];
        var email = $"{userName}@exemplo.com";
        var correlation = Guid.NewGuid().ToString("N");

        await PostAsync(factory.CreateClient(), "/api/v1/users", TestUsers.NewRegistration(userName, email), correlation, ct);

        var logEvent = factory.Logs.WithProperty("correlation_id", correlation)
            .Single(e => e.MessageTemplate.Text.StartsWith("User {UserId} registered", StringComparison.Ordinal));
        logEvent.Level.ShouldBe(LogEventLevel.Information);
        AssertField(logEvent, "UserName", userName[..3] + "***", PersonalDataKind.Login, userName);
        AssertField(logEvent, "Email", userName[..3] + "***@exemplo", PersonalDataKind.Login, email);
        AssertField(logEvent, "MobilePhone", "16*****4567", PersonalDataKind.Phone, "16991234567");
        AssertNoClearValue(logEvent, userName, email, "99123-4567", "16991234567");
    }

    [Fact]
    public async Task Register_WhenRejected_LogsOnlyErrorCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var email = $"dup.{Guid.NewGuid():N}@exemplo.com";
        var correlation = Guid.NewGuid().ToString("N");

        await PostAsync(client, "/api/v1/users", TestUsers.NewRegistration(userName, email), correlation, ct);

        var events = factory.Logs.WithProperty("correlation_id", correlation);
        events.ShouldNotBeEmpty();
        foreach (var logEvent in events)
        {
            foreach (var field in new[] { "UserName", "Email", "MobilePhone" })
            {
                logEvent.Properties.ContainsKey(field).ShouldBeFalse();
            }

            AssertNoClearValue(logEvent, userName, email, "99123-4567");
        }
    }

    [Fact]
    public async Task Login_WhenSucceeds_LogsMaskedUserNameWithToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var correlation = Guid.NewGuid().ToString("N");

        await PostAsync(client, "/api/v1/auth/login", new { userName, password = TestUsers.Password }, correlation, ct);

        var logEvent = SingleStartingWith(correlation, "User {UserId} signed in");
        logEvent.Level.ShouldBe(LogEventLevel.Information);
        AssertField(logEvent, "UserName", userName[..3] + "***", PersonalDataKind.Login, userName);
        AssertNoClearValue(logEvent, userName, TestUsers.Password);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownUser_LogsMaskedUserNameAndKeepsGenericResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var known = await TestUsers.RegisterAsync(client, ct);
        var unknown = $"inexistente.{Guid.NewGuid():N}"[..16];
        var knownCorrelation = Guid.NewGuid().ToString("N");
        var unknownCorrelation = Guid.NewGuid().ToString("N");

        var knownBody = await PostAsync(client, "/api/v1/auth/login", new { userName = known, password = "Errada@123" }, knownCorrelation, ct);
        var unknownBody = await PostAsync(client, "/api/v1/auth/login", new { userName = unknown, password = "Errada@123" }, unknownCorrelation, ct);

        // Sem enumeração de contas: a resposta é idêntica para usuário existente e inexistente.
        WithoutTraceId(unknownBody).ShouldBe(WithoutTraceId(knownBody));
        var knownEvent = SingleStartingWith(knownCorrelation, "Sign-in failed");
        var unknownEvent = SingleStartingWith(unknownCorrelation, "Sign-in failed");
        knownEvent.Level.ShouldBe(LogEventLevel.Warning);
        unknownEvent.Level.ShouldBe(LogEventLevel.Warning);
        AssertField(knownEvent, "UserName", known[..3] + "***", PersonalDataKind.Login, known);
        AssertField(unknownEvent, "UserName", unknown[..3] + "***", PersonalDataKind.Login, unknown);
        AssertNoClearValue(unknownEvent, unknown, "Errada@123");
    }

    [Fact]
    public async Task Login_WhenLockedOut_LogsMaskedUserNameAsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var correlations = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid().ToString("N")).ToList();

        foreach (var correlation in correlations)
        {
            await PostAsync(client, "/api/v1/auth/login", new { userName, password = "Errada@123" }, correlation, ct);
        }

        var locked = SingleStartingWith(correlations[^1], "Sign-in rejected: user {UserId} is locked out");
        locked.Level.ShouldBe(LogEventLevel.Warning);
        AssertField(locked, "UserName", userName[..3] + "***", PersonalDataKind.Login, userName);
        AssertNoClearValue(locked, userName);
    }

    [Fact]
    public async Task Login_WithUserNameOutOfFormat_LogsRedactedWithoutToken()
    {
        var ct = TestContext.Current.CancellationToken;
        // Texto curto demais (< MinLoginLength) para ser um login válido, como uma senha digitada no campo errado.
        const string typedPassword = "Zq9";
        var correlation = Guid.NewGuid().ToString("N");

        await PostAsync(factory.CreateClient(), "/api/v1/auth/login", new { userName = typedPassword, password = "Errada@123" }, correlation, ct);

        var logEvent = SingleStartingWith(correlation, "Sign-in failed");
        CollectingLogSink.Scalar(logEvent.Properties["UserName"]).ShouldBe(PersonalDataMasker.Redacted);
        logEvent.Properties.ContainsKey("UserName" + PersonalDataFields.TokenSuffix).ShouldBeFalse();
        AssertNoClearValue(logEvent, typedPassword);
    }

    [Fact]
    public async Task Gelf_RegisterAndLogin_CarryOnlyMasksAndTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gelf = new GelfTcpCapture();
        await using var host = GraylogHost(gelf);
        var tokens = host.Services.GetRequiredService<PseudonymTokenGenerator>();
        var client = host.CreateClient();
        var userName = $"gelf.{Guid.NewGuid():N}"[..13];
        var email = $"{userName}@exemplo.com";

        await PostAsync(client, "/api/v1/users", TestUsers.NewRegistration(userName, email), null, ct);
        await PostAsync(client, "/api/v1/auth/login", new { userName, password = TestUsers.Password }, null, ct);

        var registered = await gelf.WaitForAsync("registered (user name", GelfTimeout, ct);
        var signedIn = await gelf.WaitForAsync("signed in (user name", GelfTimeout, ct);
        foreach (var raw in new[] { registered, signedIn })
        {
            foreach (var clear in new[] { userName, email, "99123-4567", "16991234567", "(16) 99123-4567", TestUsers.Password })
            {
                raw.ShouldNotContain(clear, Case.Insensitive, $"GELF leaked '{clear}'");
            }
        }

        var login = tokens.Generate(PersonalDataKind.Login, userName);
        var fields = GelfTcpCapture.Fields(registered);
        fields["_UserName"].ShouldBe(userName[..3] + "***");
        fields["_UserName_token"].ShouldBe(login);
        fields["_UserName_token"].ShouldStartWith("k1-");
        fields["_Email"].ShouldBe(userName[..3] + "***@exemplo");
        fields["_Email_token"].ShouldBe(tokens.Generate(PersonalDataKind.Login, email));
        fields["_Email_token"].ShouldStartWith("k1-");
        fields["_MobilePhone"].ShouldBe("16*****4567");
        fields["_MobilePhone_token"].ShouldBe(tokens.Generate(PersonalDataKind.Phone, "16991234567"));
        fields["_MobilePhone_token"].ShouldStartWith("k1-");
        GelfTcpCapture.Fields(signedIn)["_UserName_token"].ShouldBe(login);
        // O texto livre leva só a máscara, nunca o token.
        foreach (var text in fields.Where(f => !f.Key.EndsWith(PersonalDataFields.TokenSuffix, StringComparison.Ordinal)))
        {
            text.Value.ShouldNotContain("k1-", Case.Sensitive, $"token leaked into {text.Key}");
        }
    }

    [Fact]
    public async Task Gelf_LoginWithUserNameOutOfFormat_CarriesRedactedWithoutToken()
    {
        var ct = TestContext.Current.CancellationToken;
        const string typedPassword = "Zq9";
        await using var gelf = new GelfTcpCapture();
        await using var host = GraylogHost(gelf);

        await PostAsync(host.CreateClient(), "/api/v1/auth/login", new { userName = typedPassword, password = "Errada@123" }, null, ct);

        var raw = await gelf.WaitForAsync("Sign-in failed", GelfTimeout, ct);
        raw.ShouldNotContain(typedPassword, Case.Sensitive);
        var fields = GelfTcpCapture.Fields(raw);
        fields["_UserName"].ShouldBe(PersonalDataMasker.Redacted);
        fields.ShouldNotContainKey("_UserName" + PersonalDataFields.TokenSuffix);
    }

    // Contrato: placeholder que carrega dado pessoal precisa ter o nome exato de PersonalDataFields, senão sairia em claro.
    [Fact]
    public void LogTemplates_PersonalPlaceholders_AreInPersonalDataFields()
    {
        var parser = new MessageTemplateParser();
        var templates = typeof(ILoginHandler).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetCustomAttributes<LoggerMessageAttribute>())
            .Select(a => a.Message!)
            .ToList();
        var placeholders = templates
            .SelectMany(t => parser.Parse(t).Tokens.OfType<PropertyToken>().Select(p => p.PropertyName))
            .Distinct()
            .ToList();

        ExpectedPlaceholders.ShouldBeSubsetOf(placeholders);
        var personalLooking = placeholders.Where(name => PersonalHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)));
        foreach (var name in personalLooking)
        {
            PersonalDataFields.ByName.ContainsKey(name).ShouldBeTrue($"placeholder '{name}' is not in PersonalDataFields and would leak in clear");
        }
    }

    private static readonly string[] ExpectedPlaceholders = ["UserName", "Email", "MobilePhone"];

    private static readonly string[] PersonalHints = ["username", "user_name", "email", "phone", "celular", "telefone", "cpf", "cnpj", "login"];

    private static void AssertField(LogEvent logEvent, string field, string mask, PersonalDataKind kind, string rawValue)
    {
        CollectingLogSink.Scalar(logEvent.Properties[field]).ShouldBe(mask);
        var token = CollectingLogSink.Scalar(logEvent.Properties[field + PersonalDataFields.TokenSuffix]).ShouldNotBeNull();
        token.ShouldStartWith("k1-");
        PersonalDataMasker.TryCanonicalize(kind, rawValue, out var canonical).ShouldBeTrue();
        canonical.ShouldNotBeNull();
    }

    private static void AssertNoClearValue(LogEvent logEvent, params string[] clearValues)
    {
        var text = CollectingLogSink.FullText(logEvent);
        foreach (var clear in clearValues)
        {
            text.ShouldNotContain(clear, Case.Insensitive, $"log event leaked '{clear}'");
            logEvent.RenderMessage(Invariant).ShouldNotContain(clear, Case.Insensitive);
        }

        // O token só vai nos campos *_token, nunca no texto renderizado.
        logEvent.RenderMessage(Invariant).ShouldNotContain("k1-");
    }

    // O traceId do ProblemDetails é único por requisição; o resto da resposta deve ser idêntico.
    private static string WithoutTraceId(string body) => System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", string.Empty);

    private LogEvent SingleStartingWith(string correlation, string prefix) =>
        factory.Logs.WithProperty("correlation_id", correlation)
            .Single(e => e.MessageTemplate.Text.StartsWith(prefix, StringComparison.Ordinal));

    private static async Task<string> PostAsync(HttpClient client, string path, object body, string? correlation, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (correlation is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlation);
        }

        using var response = await client.SendAsync(request, ct);
        return $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}";
    }

    private WebApplicationFactory<Program> GraylogHost(GelfTcpCapture gelf) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Graylog:Host", "127.0.0.1");
            builder.UseSetting("Graylog:Port", gelf.Port.ToString(Invariant));
        });
}
