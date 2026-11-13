using Legado.Domain.Common;

namespace Legado.Application.Users.Migration;

/// <summary>Pedido do Keycloak para registrar que a conta passou a ter senha no Keycloak.</summary>
/// <param name="LegacyUserId">Id da conta no Legado.</param>
/// <param name="KeycloakUserId">Id do usuário no Keycloak.</param>
public sealed record MarkUserMigratedCommand(Guid LegacyUserId, Guid KeycloakUserId);

/// <summary>Caso de uso: marcar a conta como migrada (RN-07 da spec 001 do cadastro).</summary>
public interface IMarkUserMigratedHandler
{
    /// <summary>Marca a conta; idempotente para o mesmo usuário do Keycloak.</summary>
    Task<Result> HandleAsync(MarkUserMigratedCommand command, CancellationToken cancellationToken);
}

/// <summary>Delega à porta de escrita, que grava e encerra as sessões abertas no Legado.</summary>
internal sealed class MarkUserMigratedHandler(IUserMigrationWriter writer) : IMarkUserMigratedHandler
{
    /// <inheritdoc />
    public Task<Result> HandleAsync(MarkUserMigratedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return writer.MarkMigratedAsync(command.LegacyUserId, command.KeycloakUserId, cancellationToken);
    }
}
