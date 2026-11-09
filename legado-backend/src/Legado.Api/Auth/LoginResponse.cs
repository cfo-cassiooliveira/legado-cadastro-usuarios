namespace Legado.Api.Auth;

/// <summary>Resposta de login bem-sucedido (o fingerprint vai só no cookie HttpOnly, nunca no corpo).</summary>
/// <param name="AccessToken">JWT de acesso.</param>
/// <param name="TokenType">Tipo do token (sempre "Bearer").</param>
/// <param name="ExpiresAt">Instante de expiração (UTC).</param>
internal sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);
