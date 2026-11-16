using System.Globalization;
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
        message.ShouldContain("a***@exemplo"); // Parte local com até 3 caracteres mostra só 1 (ADR-0002).
        message.ShouldContain("***.456.789-**");
    }

    [Theory]
    [InlineData("ana.souza@gmail.com", "ana***@gmail")]
    [InlineData("123.456.789-09", "***.456.789-**")]
    [InlineData("+55 (11) 98765-4321", "11*****4321")]
    public async Task PersonalData_InMessageStackAndSource_IsLoggedOnlyWithAdr0002Mask(string value, string mask)
    {
        var correlation = NewId();

        await PostAsync(new
        {
            level = "error",
            @event = "app.unhandled_error",
            message = $"Error: falhou para {value}",
            source = $"/users/{value}/profile",
            stack = $"Error: falhou para {value}\n    at main.js:1:2",
        }, correlation);

        var logged = ClientEvent(correlation);
        string[] texts =
        [
            CollectingLogSink.Scalar(logged.Properties["ClientMessage"])!,
            CollectingLogSink.Scalar(logged.Properties["ClientSource"])!,
            CollectingLogSink.Scalar(logged.Properties["ClientStack"])!,
            logged.RenderMessage(CultureInfo.InvariantCulture),
        ];
        texts.ShouldAllBe(text => !text.Contains(value) && text.Contains(mask));
    }


    [Theory]
    [InlineData("a", 5000)]
    [InlineData("a.", 5000)]
    [InlineData("%40", 3000)]
    [InlineData("a@a.", 3000)]
    [InlineData("1", 5000)]
    [InlineData("1.", 5000)]
    public async Task PathologicalText_IsAcceptedWithoutServerError(string unit, int repeat)
    {
        var correlation = NewId();

        var response = await PostAsync(new
        {
            level = "error",
            @event = "app.unhandled_error",
            message = string.Concat(Enumerable.Repeat(unit, repeat)),
            source = string.Concat(Enumerable.Repeat(unit, repeat)),
            stack = string.Concat(Enumerable.Repeat(unit, repeat)),
        }, correlation);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        ClientEvent(correlation).ShouldNotBeNull();
    }
    [Fact]
    public async Task Source_DropsQueryStringAndFragment_AndMasksEncodedEmail()
    {
        var correlation = NewId();

        await PostAsync(new
        {
            level = "warning",
            @event = "auth.sign_in_rejected",
            message = "x",
            source = "/users/ana.souza%40gmail.com?cpf=12345678909&login=maria.silva#phone=11987654321",
        }, correlation);

        CollectingLogSink.Scalar(ClientEvent(correlation).Properties["ClientSource"]).ShouldBe("/users/ana***@gmail");
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
