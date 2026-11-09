using System.Text.RegularExpressions;
using Legado.Domain.Common;

namespace Legado.Domain.Users;

/// <summary>Nome completo: ao menos nome e sobrenome; apenas letras, espaço, hífen e apóstrofo.</summary>
public sealed partial record FullName
{
    /// <summary>Tamanho máximo do nome completo.</summary>
    public const int MaxLength = 150;
    private const string FieldName = "fullName";

    private FullName(string value) => Value = value;

    /// <summary>Nome completo com espaços normalizados.</summary>
    public string Value { get; }

    /// <summary>Cria o nome completo exigindo sobrenome e apenas caracteres de nome.</summary>
    public static Result<FullName> Create(string? input)
    {
        // Normaliza espaços múltiplos para evitar duplicidade visual de nomes.
        var normalized = string.Join(' ', (input ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length == 0)
        {
            return Fail("full_name.required", "Full name is required.");
        }

        if (normalized.Length > MaxLength)
        {
            return Fail("full_name.too_long", "Full name is too long.");
        }

        if (!AllowedCharacters().IsMatch(normalized))
        {
            return Fail("full_name.invalid_characters", "Full name has invalid characters.");
        }

        return normalized.Contains(' ', StringComparison.Ordinal)
            ? Result.Success(new FullName(normalized))
            : Fail("full_name.surname_required", "Full name must include a surname.");
    }

    /// <summary>Representação textual: o nome completo.</summary>
    public override string ToString() => Value;

    private static Result<FullName> Fail(string code, string description) =>
        Result.Failure<FullName>(new DomainError(code, FieldName, description));

    [GeneratedRegex(@"^\p{L}[\p{L}'\- ]*$")]
    private static partial Regex AllowedCharacters();
}
