using System.Security.Cryptography;
using System.Text;

namespace Legado.Application.Auth;

/// <summary>
/// Hash SHA-256 usado para colocar no token apenas a "impressão" de valores sensíveis (fingerprint e security stamp),
/// nunca o valor em si.
/// </summary>
public static class TokenHash
{
    /// <summary>Calcula o SHA-256 do texto em hexadecimal minúsculo.</summary>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Compara o hash do valor com o hash esperado em tempo constante (evita ataque por tempo).</summary>
    public static bool Matches(string? value, string? expectedHash)
    {
        if (value is null || expectedHash is null)
        {
            return false;
        }

        var actual = Encoding.ASCII.GetBytes(Sha256Hex(value));
        var expected = Encoding.ASCII.GetBytes(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
