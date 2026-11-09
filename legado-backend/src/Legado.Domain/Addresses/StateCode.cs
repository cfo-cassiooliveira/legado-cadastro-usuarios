using Legado.Domain.Common;

namespace Legado.Domain.Addresses;

/// <summary>Sigla de Unidade Federativa (UF) brasileira.</summary>
public sealed record StateCode
{
    private const string FieldName = "state";

    private static readonly HashSet<string> ValidCodes =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    private StateCode(string value) => Value = value;

    /// <summary>Sigla da UF em maiúsculas (ex.: SP).</summary>
    public string Value { get; }

    /// <summary>Cria a UF validando contra a lista oficial de 27 unidades federativas.</summary>
    public static Result<StateCode> Create(string? input)
    {
        var normalized = (input ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return Result.Failure<StateCode>(new DomainError("state.required", FieldName, "State is required."));
        }

        return ValidCodes.Contains(normalized)
            ? Result.Success(new StateCode(normalized))
            : Result.Failure<StateCode>(new DomainError("state.invalid", FieldName, "State is invalid."));
    }

    /// <summary>Representação textual: a sigla da UF.</summary>
    public override string ToString() => Value;
}
