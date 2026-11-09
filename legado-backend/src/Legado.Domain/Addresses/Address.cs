using Legado.Domain.Common;

namespace Legado.Domain.Addresses;

/// <summary>Endereço residencial do usuário.</summary>
public sealed record Address
{
    /// <summary>Tamanho máximo do logradouro.</summary>
    public const int StreetMaxLength = 200;
    /// <summary>Tamanho máximo do número do imóvel.</summary>
    public const int NumberMaxLength = 20;
    /// <summary>Tamanho máximo do complemento.</summary>
    public const int ComplementMaxLength = 100;
    /// <summary>Tamanho máximo do bairro.</summary>
    public const int DistrictMaxLength = 100;
    /// <summary>Tamanho máximo do nome do município.</summary>
    public const int CityMaxLength = 100;

    private Address(PostalCode postalCode, string street, string number, string? complement, string district, City city, StateCode state)
    {
        PostalCode = postalCode;
        Street = street;
        Number = number;
        Complement = complement;
        District = district;
        City = city;
        State = state;
    }

    /// <summary>CEP do endereço.</summary>
    public PostalCode PostalCode { get; }
    /// <summary>Logradouro (rua, avenida, praça...).</summary>
    public string Street { get; }
    /// <summary>Número do imóvel (texto, pois aceita "s/n" ou "100A").</summary>
    public string Number { get; }
    /// <summary>Complemento opcional (apto, bloco, sala).</summary>
    public string? Complement { get; }
    /// <summary>Bairro.</summary>
    public string District { get; }
    /// <summary>Município identificado pelo código IBGE.</summary>
    public City City { get; }
    /// <summary>Unidade federativa.</summary>
    public StateCode State { get; }

    /// <summary>Cria o endereço validando campos obrigatórios e tamanhos; retorna todos os erros encontrados.</summary>
    public static Result<Address> Create(
        PostalCode postalCode, string? street, string? number, string? complement, string? district, City city, StateCode state)
    {
        ArgumentNullException.ThrowIfNull(postalCode);
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(state);

        var errors = new List<DomainError>();
        var normalizedStreet = RequiredText(street, "street", StreetMaxLength, errors);
        var normalizedNumber = RequiredText(number, "number", NumberMaxLength, errors);
        var normalizedDistrict = RequiredText(district, "district", DistrictMaxLength, errors);
        var normalizedComplement = string.IsNullOrWhiteSpace(complement) ? null : complement.Trim();

        if (normalizedComplement is { Length: > ComplementMaxLength })
        {
            errors.Add(new DomainError("complement.too_long", "complement", "Complement is too long."));
        }

        return errors.Count > 0
            ? Result.Failure<Address>(errors)
            : Result.Success(new Address(postalCode, normalizedStreet, normalizedNumber, normalizedComplement, normalizedDistrict, city, state));
    }

    private static string RequiredText(string? value, string field, int maxLength, List<DomainError> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors.Add(new DomainError($"{field}.required", field, $"Field '{field}' is required."));
        }
        else if (trimmed.Length > maxLength)
        {
            errors.Add(new DomainError($"{field}.too_long", field, $"Field '{field}' is too long."));
        }

        return trimmed;
    }
}
