using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;

namespace Legado.Api.Common;

/// <summary>Serilog com logs estruturados em JSON no console e envio GELF opcional para o Graylog.</summary>
internal static class LoggingSetup
{
    /// <summary>Nome do serviço em todos os logs (campo <c>service_name</c> usado pelos streams do Graylog).</summary>
    public const string ServiceName = "legado-api";

    private const int DefaultGelfPort = 12201;
    private const int StackTraceDepth = 30;
    private const string MessageTemplateField = "message_template";

    /// <summary>Registra o Serilog com enriquecimento padrão (serviço, versão, ambiente, trace) e sinks configurados.</summary>
    public static IServiceCollection AddLegacyLogging(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) =>
        services.AddSerilog((sp, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty("service_name", ServiceName)
                .Enrich.WithProperty("service_version", typeof(LoggingSetup).Assembly.GetName().Version?.ToString() ?? "unknown")
                .Enrich.WithProperty("environment", environment.EnvironmentName)
                .WriteTo.Console(new CompactJsonFormatter());

            // Graylog é opcional: habilitado quando "Graylog:Host" está configurado (docker-compose.graylog.yml).
            if (BuildGraylogOptions(configuration) is { } graylog)
            {
                logger.WriteTo.Graylog(graylog);
            }
        },
        // Não usa nem substitui o Log.Logger estático: cada host usa só a própria instância (sem estado global,
        // Art. III.3). Sem isso, um host descartado fechava o logger estático e silenciava os demais.
        preserveStaticLogger: true);

    /// <summary>
    /// Monta as opções do sink GELF a partir de <c>Graylog:*</c>; <c>null</c> quando o host não está configurado.
    /// TCP por padrão (não perde mensagens como o UDP), com o template da mensagem para agrupar eventos iguais.
    /// </summary>
    public static GraylogSinkOptions? BuildGraylogOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var host = configuration["Graylog:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return new GraylogSinkOptions
        {
            HostnameOrAddress = host,
            Port = configuration.GetValue("Graylog:Port", DefaultGelfPort),
            TransportType = configuration.GetValue("Graylog:Transport", TransportType.Tcp),
            Facility = ServiceName,
            IncludeMessageTemplate = true,
            MessageTemplateFieldName = MessageTemplateField,
            StackTraceDepth = StackTraceDepth,
            ParseArrayValues = true,
        };
    }
}
