using Legado.Domain.Common;

namespace Legado.Application.Users.Migration;

/// <summary>Porta de escrita da migração de contas para o Keycloak (ADR-0005).</summary>
public interface IUserMigrationWriter
{
    /// <summary>
    /// Marca a conta como migrada para o usuário do Keycloak informado e encerra as sessões abertas no Legado.
    /// Idempotente: repetir com o mesmo usuário do Keycloak é sucesso. Retorna <see cref="ErrorType.NotFound"/>
    /// se a conta não existe e <see cref="ErrorType.Conflict"/> se ela já foi migrada para outro usuário.
    /// </summary>
    Task<Result> MarkMigratedAsync(Guid legacyUserId, Guid keycloakUserId, CancellationToken cancellationToken);
}

/// <summary>Erros previstos da marcação de migração (códigos estáveis do contrato da API interna).</summary>
public static class UserMigrationErrors
{
    /// <summary>Conta do Legado inexistente.</summary>
    public static readonly DomainError NotFound =
        new("legacy_user.not_found", "legacyUserId", "Legacy user not found.", ErrorType.NotFound);

    /// <summary>Conta já migrada para outro usuário do Keycloak.</summary>
    public static readonly DomainError AlreadyMigratedToAnotherUser =
        new("legacy_user.migrated_to_another_user", "keycloakUserId", "Legacy user already migrated to another Keycloak user.", ErrorType.Conflict);
}
