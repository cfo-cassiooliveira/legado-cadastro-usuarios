using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Legado.Api.Internal;

/// <summary>
/// Liga a API interna de migração (ADR-0005): porta HTTPS própria, autenticação HMAC exclusiva do grupo interno e
/// isolamento por porta (rota interna só na porta interna; a porta interna só serve rotas internas).
/// </summary>
internal static class InternalApiSetup
{
    /// <summary>Prefixo das rotas internas.</summary>
    public const string RoutePrefix = "/api/v1/internal";

    /// <summary>Policy: o Keycloak verifica credenciais, consulta contas e marca migrações.</summary>
    public const string MigratePolicy = "legacy-users:migrate";

    /// <summary>Policy: o Cadastro consulta se login/e-mail estão em uso.</summary>
    public const string CheckPolicy = "legacy-users:check";

    /// <summary>Registra opções, esquema HMAC e policies (mesmo desligada, para o contrato ficar validado).</summary>
    public static IServiceCollection AddInternalApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InternalApiOptions>().Bind(configuration.GetSection(InternalApiOptions.SectionName));
        services.AddMemoryCache();
        new AuthenticationBuilder(services)
            .AddScheme<AuthenticationSchemeOptions, LegadoHmacAuthenticationHandler>(LegadoHmacAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(MigratePolicy, policy => policy
                .AddAuthenticationSchemes(LegadoHmacAuthenticationHandler.SchemeName)
                .RequireClaim(LegadoHmacAuthenticationHandler.CallerClaim, "keycloak"))
            .AddPolicy(CheckPolicy, policy => policy
                .AddAuthenticationSchemes(LegadoHmacAuthenticationHandler.SchemeName)
                .RequireClaim(LegadoHmacAuthenticationHandler.CallerClaim, "cadastro"));
        return services;
    }

    /// <summary>Com a API interna ligada, o Kestrel escuta a porta pública (HTTP) e a interna (HTTPS, certificado do Legado).</summary>
    public static void ConfigureInternalEndpoints(this KestrelServerOptions kestrel, IConfiguration configuration)
    {
        var settings = configuration.GetSection(InternalApiOptions.SectionName).Get<InternalApiOptions>() ?? new InternalApiOptions();
        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.CertificatePath) || string.IsNullOrWhiteSpace(settings.CertificateKeyPath))
        {
            throw new InvalidOperationException("InternalApi is enabled but the TLS certificate paths are not configured.");
        }

        var certificate = X509Certificate2.CreateFromPemFile(settings.CertificatePath, settings.CertificateKeyPath);
        kestrel.ListenAnyIP(settings.PublicHttpPort);
        kestrel.ListenAnyIP(settings.Port, listen => listen.UseHttps(certificate, https =>
            https.SslProtocols = System.Security.Authentication.SslProtocols.Tls13));
    }

    /// <summary>Rota interna só na porta interna, e a porta interna só com rotas internas (o resto é 404).</summary>
    public static IApplicationBuilder UseInternalApiPortIsolation(this IApplicationBuilder app, IConfiguration configuration)
    {
        var settings = configuration.GetSection(InternalApiOptions.SectionName).Get<InternalApiOptions>() ?? new InternalApiOptions();
        return app.Use((context, next) =>
        {
            var isInternalRoute = context.Request.Path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase);
            var onInternalPort = settings.Enabled && context.Connection.LocalPort == settings.Port;
            if (isInternalRoute != onInternalPort)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return next(context);
        });
    }
}
