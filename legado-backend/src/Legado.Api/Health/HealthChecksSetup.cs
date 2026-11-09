using Legado.Infrastructure.Addresses;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Legado.Api.Health;

/// <summary>
/// Health checks padronizados (regra do steering de observabilidade):
/// <c>/health/live</c> (processo), <c>/health/ready</c> (dependências obrigatórias) e
/// <c>/health</c> (relatório detalhado). Endpoints anônimos, acessíveis só pela rede interna.
/// </summary>
internal static class HealthChecksSetup
{
    /// <summary>Nome do HttpClient usado para sondar o ViaCEP.</summary>
    public const string ViaCepProbeClient = "health-viacep";
    /// <summary>Nome do HttpClient usado para sondar o IBGE.</summary>
    public const string IbgeProbeClient = "health-ibge";

    // Endpoints leves e estáveis de cada serviço externo usados como sonda.
    private static readonly Uri ViaCepProbePath = new("ws/01001000/json/", UriKind.Relative);
    private static readonly Uri IbgeProbePath = new("api/v1/localidades/estados/SP", UriKind.Relative);

    /// <summary>Registra os checks de banco, processo e serviços externos com suas tags.</summary>
    public static IServiceCollection AddLegacyHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProcessHealthOptions>().Bind(configuration.GetSection(ProcessHealthOptions.SectionName));

        var addressServices = configuration.GetSection(AddressServicesOptions.SectionName);
        services.AddHttpClient(ViaCepProbeClient, c => c.BaseAddress = addressServices.GetValue<Uri>(nameof(AddressServicesOptions.ViaCepBaseUrl)));
        services.AddHttpClient(IbgeProbeClient, c => c.BaseAddress = addressServices.GetValue<Uri>(nameof(AddressServicesOptions.IbgeBaseUrl)));

        services.AddHealthChecks()
            .AddDbContextCheck<LegadoDbContext>("sqlserver", HealthStatus.Unhealthy, [HealthCheckTags.Ready])
            .AddCheck<ProcessHealthCheck>("process", HealthStatus.Degraded, [HealthCheckTags.Process])
            .Add(new HealthCheckRegistration(
                "viacep",
                sp => new ExternalServiceHealthCheck(sp.GetRequiredService<IHttpClientFactory>(), ViaCepProbeClient, ViaCepProbePath),
                HealthStatus.Degraded,
                [HealthCheckTags.External]))
            .Add(new HealthCheckRegistration(
                "ibge",
                sp => new ExternalServiceHealthCheck(sp.GetRequiredService<IHttpClientFactory>(), IbgeProbeClient, IbgeProbePath),
                HealthStatus.Degraded,
                [HealthCheckTags.External]));

        return services;
    }

    /// <summary>Mapeia <c>/health/live</c>, <c>/health/ready</c> e <c>/health</c>.</summary>
    public static IEndpointRouteBuilder MapLegacyHealthChecks(this IEndpointRouteBuilder app)
    {
        // Liveness: só confirma que o processo responde (não consulta dependências, evita reinícios em cascata).
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

        // Readiness: dependências obrigatórias (banco). Unhealthy => 503 e a instância sai do balanceamento.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = HealthResponseWriter.WriteAsync,
        }).AllowAnonymous();

        // Relatório completo para monitoramento: banco, serviços externos e métricas do processo.
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })
            .AllowAnonymous();

        return app;
    }
}
