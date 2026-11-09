using Legado.Domain.Common;

namespace Legado.Application.Auth;

/// <summary>Porta de acesso ao estado de sessão do usuário (carimbo de segurança e bloqueio).</summary>
public interface IUserSessionStore
{
    /// <summary>Lê o estado atual da sessão; <c>null</c> se o usuário não existir mais.</summary>
    Task<SessionState?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gera um novo carimbo de segurança, invalidando todos os tokens já emitidos para o usuário.
    /// Conflito de concorrência persistente no update volta como falha (Conflict), não como exceção.
    /// </summary>
    Task<Result> RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Estado de sessão persistido.</summary>
/// <param name="SecurityStamp">Carimbo de segurança vigente.</param>
/// <param name="LockoutEnd">Fim do bloqueio da conta (UTC), quando bloqueada.</param>
public sealed record SessionState(string SecurityStamp, DateTimeOffset? LockoutEnd);
