using System.Net;
using System.Net.Http.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Regra de log do workspace: exceção capturada → Error; falha de negócio esperada (credenciais, bloqueio,
/// cadastro rejeitado) → Warning; operação concluída → Information.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LogLevelPolicyTests(LegadoApiFactory factory)
{
    [Fact]
    public async Task SuccessfulRegistrationAndSignIn_AreInformation()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var registerCorrelation = NewId();
        var loginCorrelation = NewId();
        var userName = $"user.{NewId()[..8]}";

        await SendAsync(client, HttpMethod.Post, "/api/v1/users", TestUsers.NewRegistration(userName), registerCorrelation, ct);
        await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { userName, password = TestUsers.Password }, loginCorrelation, ct);

        Single(registerCorrelation, "User {UserId} registered").Level.ShouldBe(LogEventLevel.Information);
        Single(loginCorrelation, "User {UserId} signed in").Level.ShouldBe(LogEventLevel.Information);
    }

    [Fact]
    public async Task RejectedRegistration_IsWarningWithCodesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var correlation = NewId();

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/users", TestUsers.NewRegistration(userName), correlation, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var rejected = Single(correlation, "User registration rejected: {ErrorCodes}");
        rejected.Level.ShouldBe(LogEventLevel.Warning);
        rejected.Properties["ErrorCodes"].ToString().ShouldContain("user_name.taken");
        CollectingLogSink.FullText(rejected).ShouldNotContain(userName);
    }

    [Fact]
    public async Task WrongPasswordAndLockout_AreWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var correlations = Enumerable.Range(0, 6).Select(_ => NewId()).ToList();

        foreach (var correlation in correlations)
        {
            await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { userName, password = "Errada@123" }, correlation, ct);
        }

        Single(correlations[0], "Sign-in failed: invalid credentials (known user id: {UserId})").Level.ShouldBe(LogEventLevel.Warning);
        Single(correlations[^1], "Sign-in rejected: user {UserId} is locked out").Level.ShouldBe(LogEventLevel.Warning);
    }

    [Fact]
    public async Task CaughtDependencyFailure_IsErrorWithException()
    {
        var ct = TestContext.Current.CancellationToken;
        var correlation = NewId();
        factory.PostalCodes.IsUnavailable = true;
        try
        {
            await SendAsync(factory.CreateClient(), HttpMethod.Get, "/api/v1/addresses/postal-codes/01001000", null, correlation, ct);
        }
        finally
        {
            factory.PostalCodes.IsUnavailable = false;
        }

        var failure = Single(correlation, "External address service unavailable");
        failure.Level.ShouldBe(LogEventLevel.Error);
        failure.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task CaughtMalformedRequest_IsError()
    {
        var ct = TestContext.Current.CancellationToken;
        var correlation = NewId();
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = new ByteArrayContent([.. "{\"street\":\"Pra"u8, 0xE7, .. "a\"}"u8]),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Correlation-Id", correlation);

        await development.CreateClient().SendAsync(request, ct);

        var failure = Single(correlation, "Malformed request rejected with status {StatusCode}");
        failure.Level.ShouldBe(LogEventLevel.Error);
        failure.Exception.ShouldNotBeNull();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, object? body, string correlationId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Correlation-Id", correlationId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, ct);
    }

    private LogEvent Single(string correlationId, string template) =>
        factory.Logs.WithProperty("correlation_id", correlationId).Single(e => e.MessageTemplate.Text == template);

    private static string NewId() => Guid.NewGuid().ToString("N");
}
