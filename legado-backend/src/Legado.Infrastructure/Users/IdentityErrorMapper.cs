using Legado.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Legado.Infrastructure.Users;

/// <summary>Traduz erros do ASP.NET Core Identity para códigos estáveis do contrato da API.</summary>
internal static class IdentityErrorMapper
{
    /// <summary>Converte a lista de erros do Identity em erros de domínio com código e campo.</summary>
    public static IReadOnlyList<DomainError> Map(IEnumerable<IdentityError> errors) =>
        errors.Select(Map).ToList();

    private static DomainError Map(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.DuplicateUserName) =>
            new DomainError("user_name.taken", "userName", "User name is already taken.", ErrorType.Conflict),
        nameof(IdentityErrorDescriber.DuplicateEmail) =>
            new DomainError("email.taken", "email", "Email is already registered.", ErrorType.Conflict),
        nameof(IdentityErrorDescriber.InvalidUserName) =>
            new DomainError("user_name.invalid_characters", "userName", "User name has invalid characters."),
        nameof(IdentityErrorDescriber.InvalidEmail) =>
            new DomainError("email.invalid", "email", "Email is invalid."),
        nameof(IdentityErrorDescriber.PasswordTooShort) =>
            new DomainError("password.too_short", "password", "Password is too short."),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) =>
            new DomainError("password.requires_digit", "password", "Password requires a digit."),
        nameof(IdentityErrorDescriber.PasswordRequiresLower) =>
            new DomainError("password.requires_lower", "password", "Password requires a lowercase letter."),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) =>
            new DomainError("password.requires_upper", "password", "Password requires an uppercase letter."),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) =>
            new DomainError("password.requires_non_alphanumeric", "password", "Password requires a symbol."),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) =>
            new DomainError("password.requires_unique_chars", "password", "Password requires more unique characters."),
        // Erros não mapeados não devem ocorrer com a configuração atual; expomos de forma genérica.
        _ => new DomainError("account.invalid", "account", "Account data is invalid."),
    };
}
