using System.Security.Claims;
using Legado.Api.Auth;
using Legado.Api.Common;
using Legado.Application.Users;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Legado.Api.Users;

/// <summary>Endpoints de usuários: autocadastro, perfil do usuário logado e listagem administrativa.</summary>
internal static class UserEndpoints
{
    private const int DefaultPageSize = 20;

    /// <summary>Mapeia <c>/api/v1/users</c> com a autorização de cada rota.</summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapPost("/", RegisterAsync)
            // Anônimo por requisito: autocadastro público. Protegido por rate limit.
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Credentials)
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .Produces<UserDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListAsync)
            .RequireAuthorization(AuthenticationSetup.UsersReadPolicy)
            .Produces<PagedResult<UserSummary>>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterUserCommand command, IRegisterUserHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        // Não há rota pública de leitura por id; o próprio usuário consulta seus dados em /me após o login.
        return result.IsSuccess
            ? TypedResults.Created((string?)null, new RegisterUserResponse(result.Value))
            : result.ToProblem();
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, IUserReadService users, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return TypedResults.Unauthorized();
        }

        // Sempre o usuário do token: não há parâmetro de id, o que elimina IDOR (OWASP A01).
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
    }

    private static async Task<IResult> ListAsync(IListUsersHandler handler, CancellationToken cancellationToken, int page = 1, int pageSize = DefaultPageSize)
    {
        var result = await handler.HandleAsync(page, pageSize, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
