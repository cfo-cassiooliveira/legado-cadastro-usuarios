namespace Legado.Application.Auth;

/// <summary>Porta de emissão do token de acesso do sistema legado.</summary>
public interface ITokenIssuer
{
    /// <summary>Emite o token de acesso com as claims do usuário autenticado e um fingerprint novo.</summary>
    AccessToken Issue(AuthenticatedUser user);
}

/// <summary>Token de acesso emitido.</summary>
/// <param name="Token">JWT assinado (contém apenas o hash do fingerprint).</param>
/// <param name="ExpiresAt">Instante de expiração (UTC).</param>
/// <param name="Fingerprint">
/// Valor aleatório que vai para um cookie HttpOnly; sem ele o token é recusado (proteção contra roubo de token).
/// Nunca é devolvido no corpo da resposta nem logado.
/// </param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt, string Fingerprint);
