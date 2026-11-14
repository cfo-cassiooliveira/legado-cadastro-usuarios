using Legado.Application.Users.Migration;
using Legado.Domain.Common;
using Legado.Infrastructure.Persistence;
using Legado.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Legado.Infrastructure.Users;

/// <summary>
/// Grava a migração da conta (<c>MigratedAt</c> + <c>KeycloakUserId</c>) e renova o <c>SecurityStamp</c> pelo
/// Identity, o que invalida na hora todos os tokens do Legado daquela conta (ADR-0001/0005).
/// </summary>
internal sealed partial class UserMigrationWriter(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    ILogger<UserMigrationWriter> logger) : IUserMigrationWriter
{
    // Conflito de concorrência (ConcurrencyStamp) é previsto: recarrega e tenta de novo antes de desistir.
    private const int MaxAttempts = 3;

    /// <summary>Marca a conta como migrada; idempotente para o mesmo usuário do Keycloak.</summary>
    public async Task<Result> MarkMigratedAsync(Guid legacyUserId, Guid keycloakUserId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var user = await userManager.FindByIdAsync(legacyUserId.ToString());
            if (user is null)
            {
                LogNotFound(legacyUserId);
                return Result.Failure(UserMigrationErrors.NotFound);
            }

            if (user.MigratedAt is not null)
            {
                return CompareExistingMigration(user, keycloakUserId);
            }

            user.MigratedAt = timeProvider.GetUtcNow();
            user.KeycloakUserId = keycloakUserId;
            var updated = await TryUpdateAsync(user, legacyUserId);
            if (updated is { } result)
            {
                return result;
            }

            if (attempt == MaxAttempts)
            {
                throw new InvalidOperationException($"Legacy user {legacyUserId} could not be marked as migrated after {MaxAttempts} concurrent attempts.");
            }
        }
    }

    // null = conflito de concorrência (tentar de novo); senão, o resultado final.
    private async Task<Result?> TryUpdateAsync(ApplicationUser user, Guid legacyUserId)
    {
        try
        {
            // UpdateSecurityStampAsync grava as colunas novas e o carimbo novo numa única atualização.
            var updated = await userManager.UpdateSecurityStampAsync(user);
            if (updated.Succeeded)
            {
                LogMarked(legacyUserId, user.KeycloakUserId!.Value);
                return Result.Success();
            }

            if (updated.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                LogConcurrencyConflict(legacyUserId);
                return null;
            }

            throw new InvalidOperationException($"Identity rejected the migration of legacy user {legacyUserId}: {string.Join(", ", updated.Errors.Select(e => e.Code))}.");
        }
        catch (DbUpdateException ex) when (DatabaseErrors.IsUniqueViolation(ex, out var indexName, out var sqlErrorNumber)
                                            && indexName == ApplicationUserConfiguration.KeycloakUserIdIndex)
        {
            // Outro usuário do Legado já aponta para esta conta do Keycloak. Sem a exceção no log (valor do índice).
            LogKeycloakUserAlreadyLinked(legacyUserId, sqlErrorNumber);
            return Result.Failure(UserMigrationErrors.AlreadyMigratedToAnotherUser);
        }
    }

    private Result CompareExistingMigration(ApplicationUser user, Guid keycloakUserId)
    {
        if (user.KeycloakUserId == keycloakUserId)
        {
            return Result.Success();
        }

        LogMigratedToAnotherUser(user.Id);
        return Result.Failure(UserMigrationErrors.AlreadyMigratedToAnotherUser);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Legacy user {LegacyUserId} marked as migrated to Keycloak user {KeycloakUserId}")]
    private partial void LogMarked(Guid legacyUserId, Guid keycloakUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration rejected: legacy user {LegacyUserId} not found")]
    private partial void LogNotFound(Guid legacyUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration rejected: legacy user {LegacyUserId} already migrated to another Keycloak user")]
    private partial void LogMigratedToAnotherUser(Guid legacyUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration rejected: Keycloak user already linked to another legacy user (legacy user {LegacyUserId}, SQL error {SqlErrorNumber})")]
    private partial void LogKeycloakUserAlreadyLinked(Guid legacyUserId, int sqlErrorNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrent update while marking legacy user {LegacyUserId} as migrated; retrying")]
    private partial void LogConcurrencyConflict(Guid legacyUserId);
}
