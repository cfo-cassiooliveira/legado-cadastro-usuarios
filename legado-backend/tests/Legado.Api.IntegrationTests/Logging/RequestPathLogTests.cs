using Legado.Api.Common;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// O log de requisição nunca carrega o valor da rota (e-mail, login ou CPF digitados na URL): só o padrão da rota
/// ou, sem rota, os primeiros segmentos (LGPD, Art. IV.6).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RequestPathLogTests(LegadoApiFactory factory)
{
    private const string RequestLogTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    [Theory]
    [InlineData("/api/v1/users/maria.silva@example.com")]
    [InlineData("/api/v1/users/maria.silva%40example.com")]
    [InlineData("/api/v1/users/maria.silva")]
    [InlineData("/api/v1/users/123.456.789-09")]
    public async Task UnmatchedRouteWithPersonalData_NeverReachesTheRequestLog(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var correlationId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, path + "?email=ana@example.com");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Referrer = new Uri("https://example.com/p/ana@example.com");

        using var response = await factory.CreateClient().SendAsync(request, ct);

        var requestLog = factory.Logs.WithProperty("correlation_id", correlationId)
            .Single(e => e.MessageTemplate.Text == RequestLogTemplate);
        var text = CollectingLogSink.FullText(requestLog);
        foreach (var leaked in new[] { "maria", "silva", "example.com", "ana@", "123.456", "%40", "789-09" })
        {
            text.ShouldNotContain(leaked, Case.Insensitive);
        }

        CollectingLogSink.Scalar(requestLog.Properties["RequestPath"]).ShouldStartWith("/api/v1/");
        CollectingLogSink.Scalar(requestLog.Properties["RequestMethod"]).ShouldBe("GET");
    }

    [Fact]
    public async Task UnmatchedUsersRoute_LogsOnlyTheFirstSegmentsWithWildcard()
    {
        var ct = TestContext.Current.CancellationToken;
        var correlationId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/maria.silva@example.com");
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await factory.CreateClient().SendAsync(request, ct);

        var requestLog = factory.Logs.WithProperty("correlation_id", correlationId)
            .Single(e => e.MessageTemplate.Text == RequestLogTemplate);
        CollectingLogSink.Scalar(requestLog.Properties["RequestPath"]).ShouldBe("/api/v1/users/*");
        CollectingLogSink.Scalar(requestLog.Properties["StatusCode"]).ShouldBe(((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MatchedRoute_LogsThePatternInsteadOfTheValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/internal/users/6f1c2d3e-0000-0000-0000-000000000000/migration";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/v1/internal/users/{legacyUserId:guid}/migration"), 0, null, null));

        RequestPathSanitizer.Sanitize(context).ShouldBe("/api/v1/internal/users/{legacyUserId:guid}/migration");
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/health/ready", "/health/ready")]
    [InlineData("/api/v1/users", "/api/v1/users")]
    [InlineData("/api/v1/users/ana@example.com", "/api/v1/users/*")]
    [InlineData("/x/y/z/w/ana@example.com", "/x/y/z/*")]
    public void UnmatchedRoute_KeepsAtMostThreeSegments(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        RequestPathSanitizer.Sanitize(context).ShouldBe(expected);
    }
}
