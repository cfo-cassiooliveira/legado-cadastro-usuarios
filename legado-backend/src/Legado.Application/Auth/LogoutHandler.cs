using Legado.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Legado.Application.Auth;

/// <summary>Encerra a sessão revogando no servidor todos os tokens do usuário.</summary>
public interface ILogoutHandler
{
    /// <summary>Revoga os tokens do usuário (logout em todos os dispositivos); conflito persistente volta como falha.</summary>
    Task<Result> HandleAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Implementação do logout com revogação e auditoria.</summary>
internal sealed partial class LogoutHandler(IUserSessionStore sessionStore, ILogger<LogoutHandler> logger) : ILogoutHandler
{
    /// <summary>Gera novo carimbo de segurança para o usuário: sucesso é Information, conflito é Warning.</summary>
    public async Task<Result> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var revoked = await sessionStore.RevokeAllAsync(userId, cancellationToken);
        if (revoked.IsSuccess)
        {
            LogSignedOut(userId);
        }
        else
        {
            LogSignOutRejected(userId);
        }

        return revoked;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed out; all tokens revoked")]
    private partial void LogSignedOut(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-out of user {UserId} not completed: session revoke conflict")]
    private partial void LogSignOutRejected(Guid userId);
}
