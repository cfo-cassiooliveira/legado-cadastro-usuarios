using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Graylog;

namespace Legado.Api.Common;

/// <summary>Serilog com logs estruturados em JSON no console e sink GELF opcional para o Graylog.</summary>
internal static class LoggingSetup
{
    private const string ServiceName = "legado-api";
    private const int DefaultGelfPort = 12201;

    /// <summary>Registra o Serilog com enriquecimento padrão (serviço, versão, ambiente) e sinks configurados.</summary>
    public static IServiceCollection AddLegacyLogging(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) =>
        services.AddSerilog((sp, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service_name", ServiceName)
                .Enrich.WithProperty("service_version", typeof(LoggingSetup).Assembly.GetName().Version?.ToString() ?? "unknown")
                .Enrich.WithProperty("environment", environment.EnvironmentName)
                // Formato compacto inclui @tr/@sp (trace_id/span_id) de cada evento.
                .WriteTo.Console(new CompactJsonFormatter());

            // Graylog é opcional no ambiente de demo (ADR-0001): habilitado só quando o host é configurado.
            var graylogHost = configuration["Graylog:Host"];
            if (!string.IsNullOrWhiteSpace(graylogHost))
            {
                logger.WriteTo.Graylog(new GraylogSinkOptions
                {
                    HostnameOrAddress = graylogHost,
                    Port = configuration.GetValue("Graylog:Port", DefaultGelfPort),
                    Facility = ServiceName,
                });
            }
        });
}
