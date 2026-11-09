using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Legado.Api.Common;

/// <summary>Limites por IP para os endpoints anônimos (configuráveis em <c>RateLimiting:*</c>).</summary>
internal static partial class RateLimitPolicies
{
    /// <summary>Política de login e cadastro (proteção contra força bruta).</summary>
    public const string Credentials = "credentials";

    /// <summary>Política das consultas de CEP e municípios.</summary>
    public const string AddressLookup = "address-lookup";

    /// <summary>Política do recebimento de logs do navegador (evita inundar o Graylog).</summary>
    public const string ClientLogs = "client-logs";

    private const string LoggerCategory = "Legado.Api.RateLimiting";
    private const int DefaultCredentialsPermitsPerMinute = 10;
    private const int DefaultAddressLookupPermitsPerMinute = 60;
    private const int DefaultClientLogsPermitsPerMinute = 30;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registra as políticas de rate limit por IP para login/cadastro e consultas de endereço, e loga cada rejeição
    /// (evento de segurança, OWASP A09).
    /// </summary>
    public static IServiceCollection AddLegacyRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var credentialsPermits = configuration.GetValue("RateLimiting:CredentialsPermitsPerMinute", DefaultCredentialsPermitsPerMinute);
        var addressPermits = configuration.GetValue("RateLimiting:AddressLookupPermitsPerMinute", DefaultAddressLookupPermitsPerMinute);
        var clientLogPermits = configuration.GetValue("RateLimiting:ClientLogsPermitsPerMinute", DefaultClientLogsPermitsPerMinute);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Credentials, context => FixedWindowByIp(context, credentialsPermits));
            options.AddPolicy(AddressLookup, context => FixedWindowByIp(context, addressPermits));
            options.AddPolicy(ClientLogs, context => FixedWindowByIp(context, clientLogPermits));
        });

        // O logger vem do DI (injeção explícita), não de service locator dentro do callback.
        services.AddOptions<RateLimiterOptions>().Configure<ILoggerFactory>((options, loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger(LoggerCategory);
            options.OnRejected = (context, _) =>
            {
                LogRejected(logger, context.HttpContext.Request.Path.Value ?? string.Empty);
                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = Window, QueueLimit = 0 });

    // Sem IP no log (dado pessoal, LGPD); a rota e o correlation_id bastam para investigar.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded on {RequestPath}")]
    private static partial void LogRejected(ILogger logger, string requestPath);
}
