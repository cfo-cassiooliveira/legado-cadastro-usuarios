using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Conteúdo real dos logs que vão para o Graylog: campos ricos para investigação e nenhum segredo ou dado pessoal.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class StructuredLoggingTests(LegadoApiFactory factory)
{
    private const string RequestLogTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    [Fact]
    public async Task AuthenticatedRequest_ProducesRichRequestLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        var correlationId = NewId();
        var requestId = NewId();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Request-Id", requestId);
        request.Headers.UserAgent.ParseAdd("legado-tests/1.0");
        client.Authenticate(session);

        var response = await client.SendAsync(request, ct);
        var userId = await ReadUserIdAsync(response, ct);

        var requestLog = factory.Logs.WithProperty("correlation_id", correlationId)
            .Single(e => e.MessageTemplate.Text == RequestLogTemplate);
        requestLog.Level.ShouldBe(LogEventLevel.Information);
        Prop(requestLog, "RequestMethod").ShouldBe("GET");
        Prop(requestLog, "RequestPath").ShouldBe("/api/v1/users/me");
        Prop(requestLog, "StatusCode").ShouldBe("200");
        Prop(requestLog, "user_id").ShouldBe(userId);
        Prop(requestLog, "request_id").ShouldBe(requestId);
        Prop(requestLog, "UserAgent").ShouldBe("legado-tests/1.0");
        Prop(requestLog, "service_name").ShouldBe("legado-api");
        Prop(requestLog, "EndpointName").ShouldNotBeNullOrWhiteSpace();
        Prop(requestLog, "trace_id")!.Length.ShouldBe(32);
        Prop(requestLog, "span_id")!.Length.ShouldBe(16);
        requestLog.Properties.ShouldContainKey("Elapsed");
        requestLog.Properties.ShouldContainKey("environment");
    }

    [Fact]
    public async Task UseCaseLogs_CarryUserIdAndCorrelationId()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        client.Authenticate(session);
        var correlationId = NewId();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-Correlation-Id", correlationId);

        await client.SendAsync(request, ct);

        // Log do caso de uso (dentro da requisição) herda user_id e correlation_id do contexto.
        var logoutLog = factory.Logs.WithProperty("correlation_id", correlationId)
            .Single(e => e.MessageTemplate.Text.Contains("signed out", StringComparison.Ordinal));
        logoutLog.Properties.ShouldContainKey("user_id");
        Prop(logoutLog, "user_id").ShouldBe(Prop(logoutLog, "UserId"));
    }

    [Fact]
    public async Task Logs_NeverContainPasswordTokenFingerprintOrPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var email = $"privado.{marker}@exemplo.com";
        var wrongPassword = $"Errada@{marker}";
        var userName = $"user.{marker}";
        var registration = JsonSerializer.SerializeToNode(TestUsers.NewRegistration(userName, email))!.AsObject();
        // Nome só com letras (regra do domínio) e improvável em qualquer outro log.
        registration["fullName"] = "Fulano Sigiloso Quixotesco";
        (await client.PostAsJsonAsync("/api/v1/users", registration, ct)).EnsureSuccessStatusCode();
        await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = wrongPassword }, ct);
        var session = await TestUsers.LoginAsync(client, userName, ct);
        client.Authenticate(session);
        await client.GetAsync("/api/v1/users/me", ct);

        var forbidden = new[]
        {
            TestUsers.Password, wrongPassword, session.Token, session.FingerprintCookie.Split('=', 2)[1],
            email, userName, registration["fullName"]!.GetValue<string>(), "99123-4567", "991234567",
        };
        var allLogText = string.Join("\n", factory.Logs.Events.Select(CollectingLogSink.FullText));
        foreach (var secret in forbidden)
        {
            allLogText.ShouldNotContain(secret, Case.Insensitive, $"o log não pode conter '{secret}'");
        }
    }

    [Fact]
    public async Task FailedSignIn_IsLoggedAsSecurityWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var correlationId = NewId();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { userName = "nao.existe", password = "Errada@123" }),
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        await client.SendAsync(request, ct);

        var failure = factory.Logs.WithProperty("correlation_id", correlationId)
            .Single(e => e.MessageTemplate.Text.StartsWith("Sign-in failed", StringComparison.Ordinal));
        failure.Level.ShouldBe(LogEventLevel.Warning);
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string? Prop(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? CollectingLogSink.Scalar(value) : null;

    private static async Task<string> ReadUserIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return body.RootElement.GetProperty("id").GetString()!;
    }
}
