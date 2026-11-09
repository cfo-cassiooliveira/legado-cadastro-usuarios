using Legado.Application.Auth;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Legado.Infrastructure.Auth;

/// <summary>Verifica usuário/senha com o Identity, aplicando contagem de falhas e bloqueio temporário.</summary>
internal sealed class CredentialVerifier(UserManager<ApplicationUser> userManager) : ICredentialVerifier
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
            await userManager.AccessFailedAsync(user);
            var status = await userManager.IsLockedOutAsync(user) ? CredentialStatus.LockedOut : CredentialStatus.Invalid;
            return new CredentialVerification(status, UserId: user.Id);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var securityStamp = user.SecurityStamp ?? await userManager.GetSecurityStampAsync(user);
        var authenticated = new AuthenticatedUser(user.Id, user.UserName ?? userName, [.. roles], securityStamp);
        return new CredentialVerification(CredentialStatus.Valid, authenticated, user.Id);
    }
}
