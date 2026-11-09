namespace Legado.Application.Auth;

/// <summary>Confere, a cada requisição autenticada, se a sessão do token ainda é válida no servidor.</summary>
public interface ISessionValidator
{
    /// <summary>
    /// Retorna <c>true</c> se o usuário existe, não está bloqueado e o carimbo de segurança do token é o vigente
    /// (ou seja, não houve logout/revogação depois da emissão).
    /// </summary>
    Task<bool> IsActiveAsync(Guid userId, string? securityStampHash, CancellationToken cancellationToken);
}

/// <summary>Implementação da validação de sessão baseada no carimbo de segurança do Identity.</summary>
internal sealed class SessionValidator(IUserSessionStore sessionStore, TimeProvider timeProvider) : ISessionValidator
{
    /// <summary>Consulta o estado da sessão e compara o hash do carimbo em tempo constante.</summary>
    public async Task<bool> IsActiveAsync(Guid userId, string? securityStampHash, CancellationToken cancellationToken)
    {
        if (securityStampHash is null)
        {
            return false;
        }

        var state = await sessionStore.GetAsync(userId, cancellationToken);
        if (state is null)
        {
            return false;
        }

        // Conta bloqueada perde o acesso imediatamente, mesmo com token ainda dentro da validade.
        if (state.LockoutEnd is { } lockoutEnd && lockoutEnd > timeProvider.GetUtcNow())
        {
            return false;
        }

        return TokenHash.Matches(state.SecurityStamp, securityStampHash);
    }
}
