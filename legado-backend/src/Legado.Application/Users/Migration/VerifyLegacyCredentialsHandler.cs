using Legado.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Legado.Application.Users.Migration;

/// <summary>Pedido do Keycloak para verificar login (ou e-mail) e senha.</summary>
/// <param name="Login">Login ou e-mail digitado pelo usuário.</param>
/// <param name="Password">Senha digitada pelo usuário.</param>
public sealed record VerifyLegacyCredentialsCommand(string Login, string Password);

/// <summary>Caso de uso: o Legado decide se as credenciais valem (ADR-0005); o Keycloak nunca vê o hash.</summary>
public interface IVerifyLegacyCredentialsHandler
{
    /// <summary>Verifica as credenciais com o mesmo controle de bloqueio e tempo equalizado do login do Legado.</summary>
    Task<LegacyCredentialResult> HandleAsync(VerifyLegacyCredentialsCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Resolve e-mail → login e reaproveita o <see cref="ICredentialVerifier"/> do login do Legado (bloqueio após 5 falhas,
/// hash fictício quando a conta não existe). Só em caso válido carrega os dados da conta.
/// </summary>
internal sealed partial class VerifyLegacyCredentialsHandler(
    ILegacyUserReadService users,
    ICredentialVerifier credentialVerifier,
    ILogger<VerifyLegacyCredentialsHandler> logger) : IVerifyLegacyCredentialsHandler
{
    /// <inheritdoc />
    public async Task<LegacyCredentialResult> HandleAsync(VerifyLegacyCredentialsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var login = command.Login.Trim();

        // Login inexistente segue com o próprio texto: o verificador compara com um hash fictício (mesmo tempo).
        var userName = await users.FindUserNameAsync(login, cancellationToken) ?? login;
        var verification = await credentialVerifier.VerifyAsync(userName, command.Password, cancellationToken);

        switch (verification.Status)
        {
            case CredentialStatus.Valid:
                var found = await users.FindByLoginAsync(userName, cancellationToken)
                    ?? throw new InvalidOperationException("Verified legacy user disappeared before being read.");
                LogVerified(found.User.Id);
                return new LegacyCredentialResult(LegacyCredentialOutcome.Valid, found.User);
            case CredentialStatus.LockedOut:
                LogRejected(verification.UserId, LegacyCredentialOutcome.Locked);
                return new LegacyCredentialResult(LegacyCredentialOutcome.Locked);
            case CredentialStatus.Migrated:
                LogRejected(verification.UserId, LegacyCredentialOutcome.Migrated);
                return new LegacyCredentialResult(LegacyCredentialOutcome.Migrated);
            default:
                var outcome = verification.UserId is null ? LegacyCredentialOutcome.NotFound : LegacyCredentialOutcome.InvalidPassword;
                LogRejected(verification.UserId, outcome);
                return new LegacyCredentialResult(outcome);
        }
    }

    // Só ids técnicos: login, e-mail e senha nunca vão para o log (LGPD).
    [LoggerMessage(Level = LogLevel.Information, Message = "Legacy credentials verified for migration of user {LegacyUserId}")]
    private partial void LogVerified(Guid legacyUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legacy credentials rejected for migration ({Outcome}, known user id: {LegacyUserId})")]
    private partial void LogRejected(Guid? legacyUserId, LegacyCredentialOutcome outcome);
}
