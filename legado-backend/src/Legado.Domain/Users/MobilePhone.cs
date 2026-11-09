using System.Globalization;
using Legado.Domain.Common;

namespace Legado.Domain.Users;

/// <summary>Celular brasileiro (DDD válido + 9 dígitos iniciando em 9), exposto em E.164 (+55...).</summary>
public sealed record MobilePhone
{
    private const string FieldName = "mobilePhone";
    private const string CountryCode = "55";
    private const int NationalLength = 11;
    private const int AreaCodeLength = 2;
    private const char MobilePrefix = '9';

    // DDDs ativos no Brasil (plano de numeração da Anatel).
    private static readonly HashSet<int> ValidAreaCodes =
    [
        11, 12, 13, 14, 15, 16, 17, 18, 19,
        21, 22, 24, 27, 28,
        31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49,
        51, 53, 54, 55,
        61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79,
        81, 82, 83, 84, 85, 86, 87, 88, 89,
        91, 92, 93, 94, 95, 96, 97, 98, 99,
    ];

    private MobilePhone(string nationalNumber) => NationalNumber = nationalNumber;

    /// <summary>DDD + número, somente dígitos (11 posições).</summary>
    public string NationalNumber { get; }

    /// <summary>Número no padrão internacional E.164 (+55DDDNÚMERO), formato persistido.</summary>
    public string E164 => $"+{CountryCode}{NationalNumber}";

    /// <summary>Cria o celular a partir do texto digitado (com ou sem máscara/código do país), validando DDD e o nono dígito.</summary>
    public static Result<MobilePhone> Create(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 0)
        {
            return Fail("mobile_phone.required", "Mobile phone is required.");
        }

        // Aceita o número já com o código do país.
        if (digits.Length == NationalLength + CountryCode.Length && digits.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            digits = digits[CountryCode.Length..];
        }

        if (digits.Length != NationalLength || digits[AreaCodeLength] != MobilePrefix)
        {
            return Fail("mobile_phone.invalid", "Mobile phone is invalid.");
        }

        var areaCode = int.Parse(digits[..AreaCodeLength], CultureInfo.InvariantCulture);
        return ValidAreaCodes.Contains(areaCode)
            ? Result.Success(new MobilePhone(digits))
            : Fail("mobile_phone.invalid_area_code", "Mobile phone area code is invalid.");
    }

    /// <summary>Representação textual: o número em E.164.</summary>
    public override string ToString() => E164;

    private static Result<MobilePhone> Fail(string code, string description) =>
        Result.Failure<MobilePhone>(new DomainError(code, FieldName, description));
}
