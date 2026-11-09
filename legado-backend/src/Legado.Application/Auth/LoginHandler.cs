using Legado.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Legado.Application.Auth;

/// <summary>Credenciais informadas na tela de login.</summary>
/// <param name="UserName">Login (case-insensitive).</param>
/// <param name="Password">Senha em texto puro (nunca logada).</param>
public sealed record LoginCommand(string? UserName, string? Password);

/// <summary>Autentica usuário e senha e emite o token de acesso.</summary>
public interface ILoginHandler
{
    /// <summary>Autentica e emite o token (com fingerprint); qualquer falha retorna o mesmo erro genérico.</summary>
    Task<Result<AccessToken>> HandleAsync(LoginCommand command, CancellationToken cancellationToken);
}

/// <summary>Implementação do caso de uso de login, com auditoria dos eventos de autenticação.</summary>
internal sealed partial class LoginHandler(
    ICredentialVerifier credentialVerifier,
    ITokenIssuer tokenIssuer,
    ILogger<LoginHandler> logger) : ILoginHandler
{
    // Mesma resposta para senha errada, usuário inexistente ou bloqueado: evita enumeração de contas (OWASP A07).
    private static readonly DomainError InvalidCredentials =
        new("auth.invalid_credentials", "credentials", "Invalid user name or password.", ErrorType.Unauthorized);

    /// <summary>Verifica as credenciais, emite o token e registra sucesso, falha ou bloqueio.</summary>
    public async Task<Result<AccessToken>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.UserName) || string.IsNullOrEmpty(command.Password))
        {
            return Result.Failure<AccessToken>(InvalidCredentials);
        }

        var verification = await credentialVerifier.VerifyAsync(command.UserName.Trim(), command.Password, cancellationToken);

        switch (verification)
        {
            case { Status: CredentialStatus.Valid, User: { } user }:
                var token = tokenIssuer.Issue(user);
                LogSignedIn(user.Id);
                return Result.Success(token);

            case { Status: CredentialStatus.LockedOut }:
                LogLockedOut(verification.UserId);
                return Result.Failure<AccessToken>(InvalidCredentials);

            default:
                LogInvalidCredentials(verification.UserId);
                return Result.Failure<AccessToken>(InvalidCredentials);
        }
    }

    // Eventos de autenticação são logados para auditoria (OWASP A09), sem login/senha (LGPD).
    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in")]
    private partial void LogSignedIn(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in rejected: user {UserId} is locked out")]
    private partial void LogLockedOut(Guid? userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed: invalid credentials (known user id: {UserId})")]
    private partial void LogInvalidCredentials(Guid? userId);
}
