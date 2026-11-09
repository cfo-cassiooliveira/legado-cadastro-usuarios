using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Legado.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Legado.Api.Auth;

/// <summary>
/// Emite JWT HS256 sem refresh token (ADR-0001), vinculado a um fingerprint de cookie HttpOnly e ao carimbo de
/// segurança do usuário. O token carrega só hashes desses valores e nenhum dado pessoal além do login.
/// </summary>
internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenIssuer
{
    private const int FingerprintBytes = 32;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Gera o fingerprint aleatório e o JWT assinado com <c>sub</c>, <c>preferred_username</c>, <c>role</c>, <c>fgp</c> e <c>sstamp</c>.</summary>
    public AccessToken Issue(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(settings.AccessTokenLifetime);
        var fingerprint = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(FingerprintBytes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtClaimTypes.PreferredUserName, user.UserName),
            new(JwtClaimTypes.FingerprintHash, TokenHash.Sha256Hex(fingerprint)),
            new(JwtClaimTypes.SecurityStampHash, TokenHash.Sha256Hex(user.SecurityStamp)),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(JwtClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(settings), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt, fingerprint);
    }

    /// <summary>Cria a chave simétrica usada tanto para assinar quanto para validar o token.</summary>
    public static SymmetricSecurityKey CreateSigningKey(JwtOptions settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
