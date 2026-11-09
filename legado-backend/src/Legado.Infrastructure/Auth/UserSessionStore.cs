using Legado.Application.Auth;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Auth;

/// <summary>Estado de sessão baseado no <c>SecurityStamp</c> e no <c>LockoutEnd</c> do Identity.</summary>
internal sealed class UserSessionStore(LegadoDbContext dbContext, UserManager<ApplicationUser> userManager) : IUserSessionStore
{
    /// <summary>Lê carimbo e bloqueio com uma consulta leve, sem tracking (executada a cada requisição autenticada).</summary>
    public Task<SessionState?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new SessionState(u.SecurityStamp ?? string.Empty, u.LockoutEnd))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Atualiza o carimbo de segurança do usuário; tokens com o carimbo antigo passam a ser recusados.</summary>
    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }
    }
}
