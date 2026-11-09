using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Legado.Api.Health;

/// <summary>
/// Verifica se um serviço externo responde (ViaCEP, IBGE). Registrado com falha = Degraded:
/// sem ele o cadastro fica indisponível, mas login e consultas continuam funcionando.
/// </summary>
internal sealed class ExternalServiceHealthCheck(IHttpClientFactory httpClientFactory, string clientName, Uri probeUri) : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Faz uma requisição de sonda com timeout curto e devolve Healthy ou o status de falha do registro.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
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
            // A exceção não vai para a resposta (pode conter detalhes de rede); só o status.
            return new HealthCheckResult(context.Registration.FailureStatus, "External service is unreachable.");
        }
    }
}
