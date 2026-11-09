namespace Legado.Api.Health;

/// <summary>Tags que definem em qual endpoint cada health check participa.</summary>
internal static class HealthCheckTags
{
    /// <summary>Dependências obrigatórias: se falharem, a instância não deve receber tráfego.</summary>
    public const string Ready = "ready";

    /// <summary>Dependências externas opcionais: falha degrada, mas não tira a instância do ar.</summary>
    public const string External = "external";

    /// <summary>Métricas do processo (memória, threads, uptime).</summary>
    public const string Process = "process";
}
