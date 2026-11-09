using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Legado.Api.Health;

/// <summary>
/// Verifica se um serviço externo responde (ViaCEP, IBGE). Registrado com falha = Degraded:
/// sem ele o cadastro fica indisponível, mas login e consultas continuam funcionando.
/// O resultado fica em cache por <see cref="CacheDuration"/>: sondagens frequentes do monitoramento não geram
/// tráfego nos serviços públicos nem repetem o log de erro a cada chamada.
/// </summary>
internal sealed partial class ExternalServiceHealthCheck(
    IHttpClientFactory httpClientFactory,
    string clientName,
    Uri probeUri,
    IMemoryCache cache,
    ILogger<ExternalServiceHealthCheck> logger) : IHealthCheck
{
    /// <summary>Tempo em que o resultado da sonda é reaproveitado.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private string CacheKey => $"health-probe:{clientName}";

    /// <summary>Devolve o resultado em cache ou faz uma nova sonda e o guarda.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cache.TryGetValue(CacheKey, out HealthCheckResult cached))
        {
            return cached;
        }

        var result = await ProbeAsync(context, cancellationToken);
        cache.Set(CacheKey, result, CacheDuration);
        return result;
    }

    /// <summary>Faz uma requisição de sonda com timeout curto e devolve Healthy ou o status de falha do registro.</summary>
    private async Task<HealthCheckResult> ProbeAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync(probeUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var data = new Dictionary<string, object> { ["status_code"] = (int)response.StatusCode };
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("External service is reachable.", data)
                : new HealthCheckResult(context.Registration.FailureStatus, "External service returned an error status.", data: data);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Exceção capturada → Error no log (rastreio); na resposta do health check vai só o status, sem detalhes de rede.
            LogProbeFailed(ex, clientName);
            return new HealthCheckResult(context.Registration.FailureStatus, "External service is unreachable.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Health probe {ProbeClient} failed")]
    private partial void LogProbeFailed(Exception exception, string probeClient);
}
