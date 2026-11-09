using System.Net;
using Legado.Api.Health;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
    public async Task ExternalService_WhenUnreachable_ReportsDegradedAndLogsError()
    {
        var logger = new FakeLogger<ExternalServiceHealthCheck>();
        var check = CreateExternalCheck(new ThrowingHandler(), logger);

        var result = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Exception.ShouldBeNull();
        // Regra de log: exceção capturada é sempre Error, com a exceção para rastreio.
        logger.LatestRecord.Level.ShouldBe(LogLevel.Error);
        logger.LatestRecord.Exception.ShouldBeOfType<HttpRequestException>();
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

    [Fact]
    public async Task ExternalService_RepeatedChecks_ReuseCachedProbeResult()
    {
        var handler = new CountingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var check = CreateExternalCheck(handler, cache: cache);

        var first = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);
        var second = await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);

        handler.Calls.ShouldBe(1);
        second.Status.ShouldBe(first.Status);
    }

    [Fact]
    public async Task ExternalService_WhenUnreachable_LogsErrorOncePerCacheWindow()
    {
        var logger = new FakeLogger<ExternalServiceHealthCheck>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var check = CreateExternalCheck(new ThrowingHandler(), logger, cache);

        for (var poll = 0; poll < 5; poll++)
        {
            await check.CheckHealthAsync(ContextFor(check, HealthStatus.Degraded), TestContext.Current.CancellationToken);
        }

        logger.Collector.Count.ShouldBe(1);
    }

    private static ExternalServiceHealthCheck CreateExternalCheck(
        HttpMessageHandler handler, FakeLogger<ExternalServiceHealthCheck>? logger = null, IMemoryCache? cache = null) =>
        new(new SingleClientFactory(handler), "probe", new Uri("status", UriKind.Relative),
            cache ?? new MemoryCache(new MemoryCacheOptions()), logger ?? new FakeLogger<ExternalServiceHealthCheck>());

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

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
