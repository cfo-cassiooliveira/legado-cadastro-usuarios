using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Legado.Api.Internal;

/// <summary>
/// Formato da assinatura das chamadas internas (ADR-0005), o mesmo que o Keycloak e o Cadastro implementam:
/// <c>X-Legado-Signature: keyId=&lt;id&gt;, t=&lt;unix s&gt;, n=&lt;nonce&gt;, v1=&lt;base64(HMAC-SHA256(chave, canônico))&gt;</c>,
/// com canônico = <c>t \n n \n MÉTODO \n rota+query \n hex(SHA-256(corpo))</c>.
/// </summary>
public static class HmacSignature
{
    /// <summary>Header com a assinatura.</summary>
    public const string HeaderName = "X-Legado-Signature";

    // Nonce em base64url: 16 bytes aleatórios viram 22 caracteres; o teto evita chave de cache gigante.
    private const int MinNonceLength = 22;
    private const int MaxNonceLength = 64;

    /// <summary>Texto canônico assinado: cobre horário, nonce, método, rota e corpo.</summary>
    public static string Canonical(long timestamp, string nonce, string method, string pathAndQuery, ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(method);
        return string.Create(CultureInfo.InvariantCulture,
            $"{timestamp}\n{nonce}\n{method.ToUpperInvariant()}\n{pathAndQuery}\n{Convert.ToHexStringLower(SHA256.HashData(body))}");
    }

    /// <summary>Assinatura HMAC-SHA256 do texto canônico, em bytes.</summary>
    public static byte[] Compute(byte[] key, string canonical) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical));

    /// <summary>Nonce novo (16 bytes aleatórios em base64url) para uma chamada.</summary>
    public static string NewNonce() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Valor completo do header para a chave, horário, nonce e assinatura informados.</summary>
    public static string Header(string keyId, long timestamp, string nonce, byte[] signature) =>
        string.Create(CultureInfo.InvariantCulture, $"keyId={keyId}, t={timestamp}, n={nonce}, v1={Convert.ToBase64String(signature)}");

    /// <summary>Lê o header; <c>null</c> se faltar parte ou o formato for inválido.</summary>
    public static ParsedSignature? TryParse(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var parts = header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.Ordinal);
        if (!parts.TryGetValue("keyId", out var keyId) || !parts.TryGetValue("t", out var t)
            || !parts.TryGetValue("n", out var nonce) || !parts.TryGetValue("v1", out var v1)
            || !long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || !IsValidNonce(nonce))
        {
            return null;
        }

        try
        {
            var signature = Convert.FromBase64String(v1);
            return signature.Length > 0 ? new ParsedSignature(keyId, timestamp, nonce, signature) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsValidNonce(string nonce) =>
        nonce.Length is >= MinNonceLength and <= MaxNonceLength && Base64Url.IsValid(nonce);
}

/// <summary>Partes do header de assinatura já validadas quanto ao formato.</summary>
/// <param name="KeyId">Id da chave do chamador.</param>
/// <param name="Timestamp">Horário da assinatura (Unix, segundos).</param>
/// <param name="Nonce">Valor único por chamada (anti-reenvio).</param>
/// <param name="Signature">Assinatura HMAC recebida.</param>
public sealed record ParsedSignature(string KeyId, long Timestamp, string Nonce, byte[] Signature);
