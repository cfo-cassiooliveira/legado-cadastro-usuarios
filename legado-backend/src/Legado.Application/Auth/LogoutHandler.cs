using Microsoft.Extensions.Logging;

namespace Legado.Application.Auth;

/// <summary>Encerra a sessão revogando no servidor todos os tokens do usuário.</summary>
public interface ILogoutHandler
{
    /// <summary>Revoga os tokens do usuário (logout em todos os dispositivos).</summary>
    Task HandleAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Implementação do logout com revogação e auditoria.</summary>
internal sealed partial class LogoutHandler(IUserSessionStore sessionStore, ILogger<LogoutHandler> logger) : ILogoutHandler
{
    /// <summary>Gera novo carimbo de segurança para o usuário e registra o evento.</summary>
    public async Task HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sessionStore.RevokeAllAsync(userId, cancellationToken);
        LogSignedOut(userId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed out; all tokens revoked")]
    private partial void LogSignedOut(Guid userId);
}
