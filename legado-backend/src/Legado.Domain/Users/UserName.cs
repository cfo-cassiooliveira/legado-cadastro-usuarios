using System.Text.RegularExpressions;
using Legado.Domain.Common;

namespace Legado.Domain.Users;

/// <summary>Login do usuário: 4 a 30 caracteres entre letras minúsculas, dígitos, ponto, hífen ou sublinhado.</summary>
public sealed partial record UserName
{
    /// <summary>Tamanho mínimo do login.</summary>
    public const int MinLength = 4;
    /// <summary>Tamanho máximo do login.</summary>
    public const int MaxLength = 30;
    private const string FieldName = "userName";

    private UserName(string value) => Value = value;

    /// <summary>Login normalizado em minúsculas.</summary>
    public string Value { get; }

    /// <summary>Cria o login validando tamanho e caracteres permitidos.</summary>
    public static Result<UserName> Create(string? input)
    {
        // Login é case-insensitive no legado; guardamos sempre em minúsculas.
        var normalized = (input ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            return Fail("user_name.required", "User name is required.");
        }

        if (normalized.Length is < MinLength or > MaxLength)
        {
            return Fail("user_name.invalid_length", "User name length is invalid.");
        }

        return AllowedPattern().IsMatch(normalized)
            ? Result.Success(new UserName(normalized))
            : Fail("user_name.invalid_characters", "User name has invalid characters.");
    }

    /// <summary>Representação textual: o login.</summary>
    public override string ToString() => Value;

    private static Result<UserName> Fail(string code, string description) =>
        Result.Failure<UserName>(new DomainError(code, FieldName, description));

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
    private static partial Regex AllowedPattern();
}
