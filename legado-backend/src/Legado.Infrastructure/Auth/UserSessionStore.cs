using Legado.Application.Auth;
using Legado.Domain.Common;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Legado.Infrastructure.Auth;

/// <summary>Estado de sessão baseado no <c>SecurityStamp</c> e no <c>LockoutEnd</c> do Identity.</summary>
internal sealed partial class UserSessionStore(
    LegadoDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<UserSessionStore> logger) : IUserSessionStore
{
    // Uma nova tentativa após conflito de concorrência (outro request atualizou o usuário ao mesmo tempo).
    private const int MaxAttempts = 2;

    private static readonly DomainError RevokeConflict =
        new("session.revoke_conflict", "session", "Session could not be revoked due to a concurrent update.", ErrorType.Conflict);

    /// <summary>Lê carimbo e bloqueio com uma consulta leve, sem tracking (executada a cada requisição autenticada).</summary>
    public Task<SessionState?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new SessionState(u.SecurityStamp ?? string.Empty, u.LockoutEnd))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Atualiza o carimbo de segurança do usuário (tokens antigos passam a ser recusados). Conflito de concorrência
    /// no update é falha prevista: registra Warning e tenta de novo com o usuário recarregado.
    /// </summary>
    public async Task<Result> RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                // Usuário removido: não há mais tokens válidos para ele.
                return Result.Success();
            }

            var updated = await userManager.UpdateSecurityStampAsync(user);
            if (updated.Succeeded)
            {
                return Result.Success();
            }

            LogUpdateRejected(userId, attempt, string.Join(",", updated.Errors.Select(e => e.Code)));
            // FindByIdAsync devolve a entidade já rastreada (com o ConcurrencyStamp antigo) sem ir ao banco:
            // sem recarregar, a nova tentativa falharia pelo mesmo motivo.
            await dbContext.Entry(user).ReloadAsync(cancellationToken);
        }

        return Result.Failure(RevokeConflict);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security stamp update rejected for user {UserId} (attempt {Attempt}): {ErrorCodes}")]
    private partial void LogUpdateRejected(Guid userId, int attempt, string errorCodes);
}
