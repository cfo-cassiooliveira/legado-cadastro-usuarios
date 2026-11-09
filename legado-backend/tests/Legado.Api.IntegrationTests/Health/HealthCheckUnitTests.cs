using System.Net;
using Legado.Api.Health;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Legado.Api.IntegrationTests.Health;

/// <summary>Testes isolados dos checks customizados (sem subir a API).</summary>
public sealed class HealthCheckUnitTests
{
    [Fact]
    public async Task ExternalService_WhenServiceReturnsError_ReportsRegistrationFailureStatus()
    {
        var check = CreateExternalCheck(new StaticResponseHandler(HttpStatusCode.InternalServerError));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Data["status_code"].ShouldBe(500);
    }

    [Fact]
    public async Task ExternalService_WhenUnreachable_ReportsDegradedWithoutException()
    {
        var check = CreateExternalCheck(new ThrowingHandler());

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task ExternalService_WhenServiceResponds_ReportsHealthy()
    {
        var check = CreateExternalCheck(new StaticResponseHandler(HttpStatusCode.OK));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(100_000, HealthStatus.Healthy)]
    [InlineData(-1, HealthStatus.Degraded)]
    public async Task Process_ComparesAllocatedMemoryWithLimit(long limitMegabytes, HealthStatus expected)
    {
        var options = Options.Create(new ProcessHealthOptions { MaxAllocatedMegabytes = limitMegabytes });
        var check = new ProcessHealthCheck(options, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expected);
        result.Data.Keys.ShouldContain("working_set_mb");
        result.Data.Keys.ShouldContain("thread_count");
    }

    private static ExternalServiceHealthCheck CreateExternalCheck(HttpMessageHandler handler) =>
        new(new SingleClientFactory(handler), "probe", new Uri("status", UriKind.Relative));

    private static HealthCheckContext ContextFor(IHealthCheck check, HealthStatus failureStatus) => new()
    {
        Registration = new HealthCheckRegistration("test", check, failureStatus, tags: null),
    };

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://service.test/") };
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
