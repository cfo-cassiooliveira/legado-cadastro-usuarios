namespace Legado.Application.Auth;

/// <summary>Situação da verificação de credenciais.</summary>
public enum CredentialStatus
{
    /// <summary>Usuário e senha corretos.</summary>
    Valid,
    /// <summary>Usuário inexistente ou senha incorreta.</summary>
    Invalid,
    /// <summary>Conta bloqueada temporariamente por excesso de tentativas.</summary>
    LockedOut,
    /// <summary>Senha correta, mas a conta já foi migrada para o Keycloak (ADR-0005): não entra mais no Legado.</summary>
    Migrated,
}

/// <summary>Resultado da verificação de usuário/senha.</summary>
/// <param name="Status">Situação da verificação.</param>
/// <param name="User">Preenchido apenas quando <see cref="CredentialStatus.Valid"/>.</param>
/// <param name="UserId">Id técnico do usuário quando ele existe (para auditoria de bloqueio).</param>
public sealed record CredentialVerification(CredentialStatus Status, AuthenticatedUser? User = null, Guid? UserId = null);
