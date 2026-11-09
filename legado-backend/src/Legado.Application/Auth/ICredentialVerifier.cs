namespace Legado.Application.Auth;

/// <summary>Porta de verificação de credenciais (inclui contagem de falhas e bloqueio).</summary>
public interface ICredentialVerifier
{
    /// <summary>Confere usuário e senha, registrando falhas para o bloqueio automático.</summary>
    Task<CredentialVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken);
}
