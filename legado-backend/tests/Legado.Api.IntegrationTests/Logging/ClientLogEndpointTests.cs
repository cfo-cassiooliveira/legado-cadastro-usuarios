using System.Net;
using System.Net.Http.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>Logs vindos do navegador: nível respeitado, entrada validada e saneada (dado não confiável).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class ClientLogEndpointTests(LegadoApiFactory factory)
{
    private const string Route = "/api/v1/client-logs";
    private const string Template = "Browser {ClientEvent} at {ClientSource}: {ClientMessage}";

    [Theory]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("warning", LogEventLevel.Warning)]
    [InlineData("information", LogEventLevel.Information)]
    public async Task ValidEvent_IsLoggedWithRequestedLevel(string level, LogEventLevel expected)
    {
        var correlation = NewId();

        var response = await PostAsync(new { level, @event = "auth.sign_in_rejected", message = "Request failed", source = "login-page", status = 401 }, correlation);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var logged = ClientEvent(correlation);
        logged.Level.ShouldBe(expected);
        CollectingLogSink.Scalar(logged.Properties["log_source"]).ShouldBe("browser");
        CollectingLogSink.Scalar(logged.Properties["ClientEvent"]).ShouldBe("auth.sign_in_rejected");
        CollectingLogSink.Scalar(logged.Properties["ClientStatus"]).ShouldBe("401");
    }

    [Fact]
    public async Task Message_IsSanitizedAgainstLogInjectionAndPersonalData()
    {
        var correlation = NewId();

        await PostAsync(new
        {
            level = "error",
            @event = "app.unhandled_error",
            message = "falhou\r\nINFO forged line para ana@exemplo.com cpf 123.456.789-09",
            source = "register-page",
        }, correlation);

        var message = CollectingLogSink.Scalar(ClientEvent(correlation).Properties["ClientMessage"])!;
        message.ShouldNotContain("\n");
        message.ShouldNotContain("\r");
        message.ShouldNotContain("ana@exemplo.com");
        message.ShouldNotContain("123.456.789-09");
        message.ShouldContain("***@***");
    }

    [Fact]
    public async Task LongMessage_IsTruncated()
    {
        var correlation = NewId();

        await PostAsync(new { level = "warning", @event = "app.long_message", message = new string('x', 5000), source = "x" }, correlation);

        var message = CollectingLogSink.Scalar(ClientEvent(correlation).Properties["ClientMessage"])!;
        message.Length.ShouldBe(500);
    }

    [Theory]
    [InlineData("critical", "auth.sign_in_rejected")]
    [InlineData("error", "Not A Code")]
    [InlineData("error", "x")]
    [InlineData(null, "auth.sign_in_rejected")]
    public async Task InvalidLevelOrEvent_IsRejectedAndNotLogged(string? level, string clientEvent)
    {
        var correlation = NewId();

        var response = await PostAsync(new { level, @event = clientEvent, message = "x", source = "x" }, correlation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Logs.WithProperty("correlation_id", correlation).ShouldNotContain(e => e.MessageTemplate.Text == Template);
    }

    private async Task<HttpResponseMessage> PostAsync(object body, string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Correlation-Id", correlationId);
        return await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Evento do navegador desta requisição; se não houver, falha listando os eventos de navegador existentes.</summary>
    private LogEvent ClientEvent(string correlationId)
    {
        var browserEvents = factory.Logs.Events.Where(e => e.MessageTemplate.Text == Template).ToList();
        var match = browserEvents.Where(e =>
            e.Properties.TryGetValue("correlation_id", out var value) && CollectingLogSink.Scalar(value) == correlationId).ToList();
        if (match.Count == 1)
        {
            return match[0];
        }

        var diagnostic = string.Join("\n", browserEvents.Select(e => string.Join(", ", e.Properties.Select(p => $"{p.Key}={p.Value}"))));
        throw new InvalidOperationException($"Expected 1 browser event for {correlationId}, found {match.Count}. Browser events:\n{diagnostic}");
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
