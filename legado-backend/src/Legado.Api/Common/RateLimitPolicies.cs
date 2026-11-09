using System.Threading.RateLimiting;

namespace Legado.Api.Common;

/// <summary>Limites por IP para os endpoints anônimos (configuráveis em <c>RateLimiting:*</c>).</summary>
internal static class RateLimitPolicies
{
    /// <summary>Política de login e cadastro (proteção contra força bruta).</summary>
    public const string Credentials = "credentials";
    /// <summary>Política das consultas de CEP e municípios.</summary>
    public const string AddressLookup = "address-lookup";

    private const int DefaultCredentialsPermitsPerMinute = 10;
    private const int DefaultAddressLookupPermitsPerMinute = 60;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Registra as políticas de rate limit por IP para login/cadastro e consultas de endereço.</summary>
    public static IServiceCollection AddLegacyRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var credentialsPermits = configuration.GetValue("RateLimiting:CredentialsPermitsPerMinute", DefaultCredentialsPermitsPerMinute);
        var addressPermits = configuration.GetValue("RateLimiting:AddressLookupPermitsPerMinute", DefaultAddressLookupPermitsPerMinute);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Credentials, context => FixedWindowByIp(context, credentialsPermits));
            options.AddPolicy(AddressLookup, context => FixedWindowByIp(context, addressPermits));
        });
    }

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = Window, QueueLimit = 0 });
}
