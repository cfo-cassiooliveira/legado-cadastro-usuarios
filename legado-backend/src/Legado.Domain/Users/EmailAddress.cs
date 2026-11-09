using System.Text.RegularExpressions;
using Legado.Domain.Common;

namespace Legado.Domain.Users;

/// <summary>E-mail do usuário, normalizado em minúsculas.</summary>
public sealed partial record EmailAddress
{
    /// <summary>Tamanho máximo do e-mail (limite do Identity).</summary>
    public const int MaxLength = 256;
    private const string FieldName = "email";

    private EmailAddress(string value) => Value = value;

    /// <summary>E-mail normalizado em minúsculas.</summary>
    public string Value { get; }

    /// <summary>Cria o e-mail validando formato e tamanho.</summary>
    public static Result<EmailAddress> Create(string? input)
    {
        var normalized = (input ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            return Result.Failure<EmailAddress>(new DomainError("email.required", FieldName, "Email is required."));
        }

        // Validação estrutural simples; a confirmação real do endereço exigiria fluxo de verificação.
        return normalized.Length <= MaxLength && EmailPattern().IsMatch(normalized)
            ? Result.Success(new EmailAddress(normalized))
            : Result.Failure<EmailAddress>(new DomainError("email.invalid", FieldName, "Email is invalid."));
    }

    /// <summary>Representação textual: o e-mail normalizado.</summary>
    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
