using System.ComponentModel.DataAnnotations;

namespace Legado.Api.Auth;

/// <summary>Configuração do JWT emitido pelo próprio legado (exceção do ADR-0001).</summary>
public sealed class JwtOptions
{
    /// <summary>Seção de configuração (<c>Jwt</c>).</summary>
    public const string SectionName = "Jwt";

    /// <summary>HS256 exige chave de no mínimo 256 bits (32 bytes).</summary>
    public const int MinSigningKeyLength = 32;

    /// <summary>Emissor (<c>iss</c>) gravado e exigido na validação do token.</summary>
    [Required]
    public required string Issuer { get; init; }

    /// <summary>Público (<c>aud</c>) gravado e exigido na validação do token.</summary>
    [Required]
    public required string Audience { get; init; }

    /// <summary>Chave simétrica; vem de variável de ambiente/user-secrets, nunca do repositório.</summary>
    [Required]
    [MinLength(MinSigningKeyLength)]
    public required string SigningKey { get; init; }

    /// <summary>Validade do token de acesso (padrão 15 min; não há refresh token).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
}
