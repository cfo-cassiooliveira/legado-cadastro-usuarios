using Legado.Api.Common.Pseudonymization;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;

namespace Legado.Api.Common;

/// <summary>Serilog com logs estruturados em JSON no console e envio GELF opcional para o Graylog.</summary>
/// <remarks>
/// <para><b>Dados pessoais (LGPD)</b>: nenhum log contém dado pessoal (Constituição, Art. IV.6). Só um dado
/// <b>aprovado em ADR</b> vai ao log, e apenas como campo estruturado do message template com o nome do tipo:
/// <c>{Cpf}</c>, <c>{Cnpj}</c>, <c>{Phone}</c>/<c>{MobilePhone}</c>, <c>{UserName}</c> ou <c>{Email}</c>. Nunca
/// concatenado ou interpolado; destruturado (<c>{@Obj}</c>) também é tratado, mas não entra na consulta padrão.</para>
/// <para><b>Pseudonimização na origem</b> (ADR-0003 do projeto graylog): o <see cref="PseudonymizationEnricher"/>
/// roda depois dos demais enrichers e antes dos sinks. Cada campo cujo nome está em <see cref="PersonalDataFields"/>
/// (as mesmas listas <c>LGPD_*</c> do graylog) vira a máscara do ADR-0002 (<c>***.456.789-**</c>,
/// <c>12.3**.***/***1-90</c>, <c>11*****4321</c>, <c>mar***</c>, <c>ana***@gmail</c>) e ganha <c>&lt;Campo&gt;_token</c>
/// (<c>k1-</c> + HMAC-SHA256 truncado, para pesquisa exata; e-mail e login geram o mesmo token), também dentro de
/// objetos destruturados, dicionários e listas (token no topo, fora do texto: <c>Customer_Email_token</c>). Valor fora do formato,
/// ou objeto/lista no lugar do valor, vira <c>***</c> sem token. O texto (<c>message</c>/<c>full_message</c>) é
/// renderizado depois, a partir das propriedades já trocadas, e o texto já renderizado dos escopos (<c>Scope</c>)
/// recebe a troca do valor pela máscara válida (ou <c>***</c> para CPF/CNPJ/telefone fora do formato com 8+ dígitos);
/// login/e-mail curto ou inválido não é trocado, para não corromper palavras. <b>Não é tratado</b>: a exceção (o Serilog
/// não permite trocá-la, por isso nunca deve conter dado pessoal) e nomes fora das listas, em qualquer texto ou campo;
/// o pipeline do Graylog continua como rede de segurança.</para>
/// </remarks>
internal static class LoggingSetup
{
    /// <summary>Nome do serviço em todos os logs (campo <c>service_name</c> usado pelos streams do Graylog).</summary>
    public const string ServiceName = "legado-api";

    private const int DefaultGelfPort = 12201;
    private const int StackTraceDepth = 30;
    private const string MessageTemplateField = "message_template";

    /// <summary>Registra o Serilog com enriquecimento padrão (serviço, versão, ambiente, trace) e sinks configurados.</summary>
    public static IServiceCollection AddLegacyLogging(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Sem chave atual válida a API não sobe (fail fast), em vez de logar dado pessoal sem pseudonimizar.
        services.AddOptions<PseudonymizationOptions>()
            .Bind(configuration.GetSection(PseudonymizationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PseudonymizationOptions>, PseudonymizationOptionsValidator>();
        services.AddSingleton<PseudonymTokenGenerator>();
        services.AddSingleton<PersonalDataTextMask>(PersonalDataTextMasker.Mask);
        services.AddSingleton<PseudonymizationEnricher>();

        return services.AddSerilog((sp, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                // Depois da configuração, de propósito: appsettings ou variável de ambiente não religam esta categoria. O EF Core
                // registra como Error a falha de gravação com a mensagem do SQL Server, que traz o valor duplicado (e-mail, login).
                // A exceção continua subindo: quem a trata (escritores ou handler global) registra sem o valor.
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Update", LogEventLevel.Fatal)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty("service_name", ServiceName)
                .Enrich.WithProperty("service_version", typeof(LoggingSetup).Assembly.GetName().Version?.ToString() ?? "unknown")
                .Enrich.WithProperty("environment", environment.EnvironmentName)
                // Por último: também trata as propriedades vindas do LogContext e dos enrichers acima.
                .Enrich.With(sp.GetRequiredService<PseudonymizationEnricher>())
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
    }

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
