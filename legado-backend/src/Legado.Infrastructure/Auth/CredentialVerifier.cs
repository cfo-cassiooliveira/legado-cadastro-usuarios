using Legado.Application.Auth;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Legado.Infrastructure.Auth;

/// <summary>Verifica usuário/senha com o Identity, aplicando contagem de falhas e bloqueio temporário.</summary>
internal sealed partial class CredentialVerifier(
    UserManager<ApplicationUser> userManager,
    ILogger<CredentialVerifier> logger) : ICredentialVerifier
{
    // Hash de referência para equalizar o tempo de resposta quando o usuário não existe (evita enumeração por timing).
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    /// <summary>Valida a senha com o Identity, conta falhas, aplica bloqueio e carrega os perfis do usuário.</summary>
    public async Task<CredentialVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password);
            return new CredentialVerification(CredentialStatus.Invalid);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return new CredentialVerification(CredentialStatus.LockedOut, UserId: user.Id);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            WarnIfUpdateFailed(await userManager.AccessFailedAsync(user), user.Id, "record failed attempt");
            var status = await userManager.IsLockedOutAsync(user) ? CredentialStatus.LockedOut : CredentialStatus.Invalid;
            return new CredentialVerification(status, UserId: user.Id);
        }

        WarnIfUpdateFailed(await userManager.ResetAccessFailedCountAsync(user), user.Id, "reset failed attempts");
        if (user.MigratedAt is not null)
        {
            // Só depois da senha conferida: quem não sabe a senha não descobre que a conta foi migrada.
            return new CredentialVerification(CredentialStatus.Migrated, UserId: user.Id);
        }

        var roles = await userManager.GetRolesAsync(user);
        var securityStamp = user.SecurityStamp ?? await userManager.GetSecurityStampAsync(user);
        var authenticated = new AuthenticatedUser(user.Id, user.UserName ?? userName, [.. roles], securityStamp);
        return new CredentialVerification(CredentialStatus.Valid, authenticated, user.Id);
    }

    /// <summary>
    /// Update do contador de falhas recusado (ex.: concorrência entre logins simultâneos) é falha prevista: não
    /// muda o resultado da verificação, mas fica registrado como Warning para rastreio.
    /// </summary>
    private void WarnIfUpdateFailed(IdentityResult result, Guid userId, string operation)
    {
        if (!result.Succeeded)
        {
            LogUpdateRejected(operation, userId, string.Join(",", result.Errors.Select(e => e.Code)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in bookkeeping update rejected ({Operation}) for user {UserId}: {ErrorCodes}")]
    private partial void LogUpdateRejected(string operation, Guid userId, string errorCodes);
}
