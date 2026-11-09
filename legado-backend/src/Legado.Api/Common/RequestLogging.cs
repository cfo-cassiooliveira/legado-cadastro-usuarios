using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;
using Serilog.Events;

namespace Legado.Api.Common;

/// <summary>
/// Log de requisição único e rico por chamada HTTP (método, rota, status, latência, endpoint, usuário, ids de
/// correlação). Nunca registra corpo, query string, headers de autenticação ou cookies.
/// </summary>
internal static class RequestLogging
{
    /// <summary>Header com o id que o nginx gera por requisição (<c>$request_id</c>), para cruzar com o access log.</summary>
    public const string RequestIdHeader = "X-Request-Id";

    /// <summary>Requisições acima deste tempo (ms) são logadas como Warning (alerta "API lenta").</summary>
    public const double SlowRequestThresholdMs = 1000;

    private const string HealthPathPrefix = "/health";

    /// <summary>Adiciona o log de requisição do Serilog com nível dinâmico e enriquecimento.</summary>
    public static IApplicationBuilder UseLegacyRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Logger do próprio host (registrado pelo AddSerilog); sem isso o middleware usaria o Log.Logger estático.
        var hostLogger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
        return app.UseSerilogRequestLogging(options =>
        {
            options.Logger = hostLogger;
            options.GetLevel = GetLevel;
            options.EnrichDiagnosticContext = Enrich;
        });
    }

    /// <summary>
    /// Error para exceção/5xx, Warning para requisição lenta, Information para o resto (4xx é fluxo esperado).
    /// Health checks bem-sucedidos viram Verbose (descartados) para não inundar o Graylog com sondas periódicas.
    /// </summary>
    public static LogEventLevel GetLevel(HttpContext context, double elapsedMs, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(context);
        var status = context.Response.StatusCode;
        if (exception is not null || status >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (context.Request.Path.StartsWithSegments(HealthPathPrefix, StringComparison.OrdinalIgnoreCase)
            && status == StatusCodes.Status200OK)
        {
            return LogEventLevel.Verbose;
        }

        return elapsedMs > SlowRequestThresholdMs ? LogEventLevel.Warning : LogEventLevel.Information;
    }

    /// <summary>Acrescenta ao evento de requisição os dados que não fazem parte do template padrão.</summary>
    public static void Enrich(IDiagnosticContext diagnosticContext, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(diagnosticContext);
        ArgumentNullException.ThrowIfNull(context);

        diagnosticContext.Set("RequestHost", context.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", context.Request.Scheme);
        diagnosticContext.Set("EndpointName", context.GetEndpoint()?.DisplayName);
        diagnosticContext.Set("ResponseContentType", context.Response.ContentType);
        diagnosticContext.Set("UserAgent", context.Request.Headers.UserAgent.ToString());

        // Só aceita ids em formato seguro (evita log injection com valor vindo do cliente).
        var requestId = context.Request.Headers[RequestIdHeader].ToString();
        if (CorrelationIdMiddleware.IsSafeId(requestId))
        {
            diagnosticContext.Set("request_id", requestId);
        }

        // Id técnico do usuário (nunca login, e-mail ou nome — LGPD).
        if (context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { } userId)
        {
            diagnosticContext.Set("user_id", userId);
        }
    }
}
