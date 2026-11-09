using System.Diagnostics;
using Legado.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Graylog.Core.Transport;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>Testes isolados da configuração de logging (sem subir a API).</summary>
public sealed class LoggingConfigurationTests
{
    [Fact]
    public void GraylogOptions_WithoutHost_IsDisabled()
    {
        LoggingSetup.BuildGraylogOptions(Config()).ShouldBeNull();
    }

    [Fact]
    public void GraylogOptions_WithHost_DefaultsToTcpWithTemplateAndServiceFacility()
    {
        var options = LoggingSetup.BuildGraylogOptions(Config(("Graylog:Host", "graylog")));

        options.ShouldNotBeNull();
        options.HostnameOrAddress.ShouldBe("graylog");
        options.Port.ShouldBe(12201);
        options.TransportType.ShouldBe(TransportType.Tcp);
        options.Facility.ShouldBe(LoggingSetup.ServiceName);
        options.IncludeMessageTemplate.ShouldBeTrue();
        options.MessageTemplateFieldName.ShouldBe("message_template");
    }

    [Fact]
    public void GraylogOptions_HonorsConfiguredPortAndTransport()
    {
        var options = LoggingSetup.BuildGraylogOptions(Config(("Graylog:Host", "logs"), ("Graylog:Port", "5555"), ("Graylog:Transport", "Udp")));

        options.ShouldNotBeNull();
        options.Port.ShouldBe(5555);
        options.TransportType.ShouldBe(TransportType.Udp);
    }

    [Theory]
    [InlineData("/api/v1/users", 200, 10, false, LogEventLevel.Information)]
    [InlineData("/api/v1/users", 404, 10, false, LogEventLevel.Information)]
    [InlineData("/api/v1/users", 500, 10, false, LogEventLevel.Error)]
    [InlineData("/api/v1/users", 200, 10, true, LogEventLevel.Error)]
    [InlineData("/api/v1/users", 200, 1500, false, LogEventLevel.Warning)]
    [InlineData("/health/ready", 200, 10, false, LogEventLevel.Verbose)]
    [InlineData("/health/ready", 503, 10, false, LogEventLevel.Error)]
    public void RequestLevel_DependsOnStatusLatencyAndRoute(string path, int status, double elapsedMs, bool withException, LogEventLevel expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;

        var level = RequestLogging.GetLevel(context, elapsedMs, withException ? new InvalidOperationException("boom") : null);

        level.ShouldBe(expected);
    }

    [Fact]
    public void TraceContextEnricher_AddsTraceSpanAndThread()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("x"), []);

        new TraceContextEnricher().Enrich(logEvent, new PropertyFactory());

        logEvent.Properties["trace_id"].ToString().ShouldContain(activity.TraceId.ToHexString());
        logEvent.Properties["span_id"].ToString().ShouldContain(activity.SpanId.ToHexString());
        logEvent.Properties.ShouldContainKey("thread_id");
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private sealed class PropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
