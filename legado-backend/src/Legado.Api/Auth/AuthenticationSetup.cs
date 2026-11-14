using Legado.Application.Auth;
using Legado.Application.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Legado.Api.Auth;

/// <summary>Autenticação JWT e políticas de autorização (negar por padrão, Art. IV.3).</summary>
internal static class AuthenticationSetup
{
    /// <summary>Política que permite listar usuários (perfil Admin).</summary>
    public const string UsersReadPolicy = "users:read";
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Configura a validação do JWT (emissor, público, assinatura, algoritmo, expiração), a verificação de
    /// fingerprint e de sessão ativa, a emissão de tokens e as políticas de autorização.
    /// </summary>
    public static IServiceCollection AddLegacyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<MigrationRedirectOptions>()
            .Bind(configuration.GetSection(MigrationRedirectOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<LegacyJwtBearerEvents>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                // Mantém os nomes originais das claims (sub, role) em vez dos URIs do WS-Federation.
                bearer.MapInboundClaims = false;
                // Eventos resolvidos pelo DI (injeção por construtor, sem service locator).
                bearer.EventsType = typeof(LegacyJwtBearerEvents);
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = JwtTokenIssuer.CreateSigningKey(jwt.Value),
                    // Só HS256: bloqueia "alg: none" e troca de algoritmo.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = ClockSkew,
                    NameClaimType = JwtClaimTypes.PreferredUserName,
                    RoleClaimType = JwtClaimTypes.Role,
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(UsersReadPolicy, policy => policy.RequireRole(Roles.Admin));

        return services;
    }
}
