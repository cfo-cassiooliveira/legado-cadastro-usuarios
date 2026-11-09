using System.Security.Claims;
using Legado.Api.Common;
using Legado.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Legado.Api.Auth;

/// <summary>Endpoints de autenticação do legado.</summary>
internal static class AuthEndpoints
{
    private const string BearerTokenType = "Bearer";

    /// <summary>Mapeia <c>POST /api/v1/auth/login</c> e <c>POST /api/v1/auth/logout</c>.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            // Anônimo por natureza: é o próprio ponto de autenticação. Protegido por rate limit (força bruta).
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Credentials)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    /// <summary>Autentica, grava o fingerprint em cookie HttpOnly e devolve apenas o token no corpo.</summary>
    private static async Task<IResult> LoginAsync(
        LoginCommand command, ILoginHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var token = result.Value;
        FingerprintCookie.Append(response, token.Fingerprint, token.ExpiresAt);
        return TypedResults.Ok(new LoginResponse(token.Token, BearerTokenType, token.ExpiresAt));
    }

    /// <summary>Revoga todos os tokens do usuário no servidor e apaga o cookie de fingerprint.</summary>
    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal, ILogoutHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        // O principal já passou pela validação completa; o sub é sempre um Guid válido aqui.
        var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await handler.HandleAsync(userId, cancellationToken);
        FingerprintCookie.Delete(response);
        return TypedResults.NoContent();
    }
}
