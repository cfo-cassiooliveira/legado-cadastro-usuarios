using Legado.Domain.Common;

namespace Legado.Domain.Addresses;

/// <summary>CEP com 8 dígitos (aceita entrada com hífen ou ponto).</summary>
public sealed record PostalCode
{
    /// <summary>Quantidade de dígitos de um CEP.</summary>
    public const int Length = 8;
    private const int PrefixLength = 5;
    private const string FieldName = "postalCode";

    private PostalCode(string value) => Value = value;

    /// <summary>Somente dígitos.</summary>
    public string Value { get; }

    /// <summary>CEP com máscara (00000-000) para exibição.</summary>
    public string Formatted => $"{Value[..PrefixLength]}-{Value[PrefixLength..]}";

    /// <summary>Cria o CEP a partir do texto digitado, aceitando hífen e ponto como separadores.</summary>
    public static Result<PostalCode> Create(string? input)
    {
        var raw = (input ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return Result.Failure<PostalCode>(new DomainError("postal_code.required", FieldName, "Postal code is required."));
        }

        // Remove apenas separadores conhecidos; qualquer outro caractere invalida o CEP.
        var digits = raw.Replace("-", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        var isValid = digits.Length == Length && digits.All(char.IsAsciiDigit) && digits.Any(c => c != '0');

        return isValid
            ? Result.Success(new PostalCode(digits))
            : Result.Failure<PostalCode>(new DomainError("postal_code.invalid", FieldName, "Postal code is invalid."));
    }

    /// <summary>Representação textual: o CEP com máscara.</summary>
    public override string ToString() => Formatted;
}
