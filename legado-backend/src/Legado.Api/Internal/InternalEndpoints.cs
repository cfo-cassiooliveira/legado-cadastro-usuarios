using Legado.Api.Common;
using Legado.Application.Users.Migration;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Legado.Api.Internal;

/// <summary>
/// Endpoints internos da migração para o Keycloak (ADR-0005). Só existem com a API interna ligada, na porta interna e
/// com assinatura HMAC do chamador autorizado. Login e e-mail vão no corpo, nunca na URL (Art. IV.6).
/// </summary>
internal static class InternalEndpoints
{
    /// <summary>Mapeia os endpoints internos quando a API interna está ligada.</summary>
    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>($"{InternalApiOptions.SectionName}:{nameof(InternalApiOptions.Enabled)}"))
        {
            return app;
        }

        // Assinatura recusada (401) e chamador sem permissão (403) valem para todo o grupo.
        var group = app.MapGroup(InternalApiSetup.RoutePrefix).WithTags("Internal")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapPost("/credential-verifications", VerifyCredentialsAsync).RequireAuthorization(InternalApiSetup.MigratePolicy);
        group.MapPost("/user-lookups", LookupAsync).RequireAuthorization(InternalApiSetup.MigratePolicy);
        group.MapPut("/users/{legacyUserId:guid}/migration", MarkMigratedAsync).RequireAuthorization(InternalApiSetup.MigratePolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/user-existence-checks", CheckExistenceAsync).RequireAuthorization(InternalApiSetup.CheckPolicy);
        return app;
    }

    private static async Task<Results<Ok<CredentialVerificationResponse>, ValidationProblem>> VerifyCredentialsAsync(
        CredentialVerificationRequest request, IVerifyLegacyCredentialsHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Invalid("credentials", "credentials.required");
        }

        var result = await handler.HandleAsync(new VerifyLegacyCredentialsCommand(request.Login, request.Password), cancellationToken);
        return TypedResults.Ok(new CredentialVerificationResponse(OutcomeCode(result.Outcome.ToString()), result.User));
    }

    private static async Task<Results<Ok<UserLookupResponse>, ValidationProblem>> LookupAsync(
        UserLookupRequest request, IGetLegacyUserByLoginHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login))
        {
            return Invalid("login", "login.required");
        }

        var result = await handler.HandleAsync(request.Login, cancellationToken);
        return TypedResults.Ok(new UserLookupResponse(OutcomeCode(result.Outcome.ToString()), result.User));
    }

    private static async Task<IResult> MarkMigratedAsync(
        Guid legacyUserId, MigrationRequest request, IMarkUserMigratedHandler handler, CancellationToken cancellationToken)
    {
        if (request.KeycloakUserId is not { } keycloakUserId || keycloakUserId == Guid.Empty)
        {
            return Invalid("keycloakUserId", "keycloak_user_id.required");
        }

        var result = await handler.HandleAsync(new MarkUserMigratedCommand(legacyUserId, keycloakUserId), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }

    private static async Task<Results<Ok<LegacyUserExistence>, ValidationProblem>> CheckExistenceAsync(
        ExistenceCheckRequest request, IGetLegacyUserExistenceHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email))
        {
            return Invalid("account", "account.required");
        }

        return TypedResults.Ok(await handler.HandleAsync(request.UserName, request.Email, cancellationToken));
    }

    private static ValidationProblem Invalid(string field, string code) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [code] });

    // Contrato estável em snake_case: InvalidPassword → invalid_password.
    private static string OutcomeCode(string outcome) =>
        string.Concat(outcome.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

/// <summary>Pedido de verificação de credenciais (Keycloak).</summary>
/// <param name="Login">Login ou e-mail.</param>
/// <param name="Password">Senha digitada.</param>
internal sealed record CredentialVerificationRequest(string? Login, string? Password);

/// <summary>Resposta da verificação: <c>valid</c>, <c>not_found</c>, <c>invalid_password</c>, <c>locked</c> ou <c>migrated</c>.</summary>
/// <param name="Outcome">Resultado.</param>
/// <param name="User">Dados da conta, só em <c>valid</c>.</param>
internal sealed record CredentialVerificationResponse(string Outcome, LegacyUser? User);

/// <summary>Pedido de consulta por login ou e-mail (Keycloak, no reset de senha).</summary>
/// <param name="Login">Login ou e-mail.</param>
internal sealed record UserLookupRequest(string? Login);

/// <summary>Resposta da consulta: <c>found</c>, <c>not_found</c> ou <c>migrated</c>.</summary>
/// <param name="Outcome">Resultado.</param>
/// <param name="User">Dados da conta, só em <c>found</c>.</param>
internal sealed record UserLookupResponse(string Outcome, LegacyUser? User);

/// <summary>Pedido de marcação da migração.</summary>
/// <param name="KeycloakUserId">Id do usuário no Keycloak.</param>
internal sealed record MigrationRequest(Guid? KeycloakUserId);

/// <summary>Pedido de checagem de login/e-mail em uso (Cadastro).</summary>
/// <param name="UserName">Login desejado.</param>
/// <param name="Email">E-mail desejado.</param>
internal sealed record ExistenceCheckRequest(string? UserName, string? Email);
