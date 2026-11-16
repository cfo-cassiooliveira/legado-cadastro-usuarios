using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Legado.Api.Common.Pseudonymization;

/// <summary>
/// Gera o token pseudônimo pesquisável do ADR-0003 do graylog com a chave atual:
/// <c>"&lt;keyId&gt;-" + hex(HMAC-SHA256(chave, "&lt;domínio&gt;|" + canônico))[0..32]</c>, hex minúsculo.
/// E-mail e login usam o mesmo domínio (<c>login</c>), então geram o mesmo token.
/// </summary>
internal sealed class PseudonymTokenGenerator
{
    private const int TruncatedBytes = 16;

    private readonly byte[] _key;
    private readonly string _prefix;

    /// <summary>Carrega a chave atual; as opções já foram validadas na subida (<see cref="PseudonymizationOptionsValidator"/>).</summary>
    public PseudonymTokenGenerator(IOptions<PseudonymizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var current = options.Value;
        _key = current.Keys.TryGetValue(current.CurrentKeyId, out var encoded)
            ? PseudonymizationOptionsValidator.Decode(encoded) ?? throw MissingKey()
            : throw MissingKey();
        _prefix = current.CurrentKeyId + "-";
    }

    /// <summary>Token do valor canônico (saída de <see cref="PersonalDataMasker.TryCanonicalize"/>).</summary>
    public string Generate(PersonalDataKind kind, string canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var message = Encoding.UTF8.GetBytes($"{Domain(kind)}|{canonical}");
        var mac = HMACSHA256.HashData(_key, message);
        return _prefix + Convert.ToHexStringLower(mac, 0, TruncatedBytes);
    }

    private static string Domain(PersonalDataKind kind) => kind switch
    {
        PersonalDataKind.Cpf => "cpf",
        PersonalDataKind.Cnpj => "cnpj",
        PersonalDataKind.Phone => "phone",
        PersonalDataKind.Login => "login",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown personal data kind."),
    };

    private static InvalidOperationException MissingKey() =>
        new($"{PseudonymizationOptions.SectionName}: current key is missing or invalid.");
}
