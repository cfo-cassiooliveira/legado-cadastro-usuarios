using Legado.Domain.Addresses;

namespace Legado.Infrastructure.Persistence;

/// <summary>Endereço persistido como complex type nas colunas <c>Address*</c> de <c>AspNetUsers</c>.</summary>
public sealed class UserAddress
{
    /// <summary>CEP com 8 dígitos.</summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Logradouro.</summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>Número do imóvel.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Complemento opcional.</summary>
    public string? Complement { get; set; }

    /// <summary>Bairro.</summary>
    public string District { get; set; } = string.Empty;

    /// <summary>Nome do município.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Código IBGE do município.</summary>
    public int CityIbgeCode { get; set; }

    /// <summary>Sigla da UF.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Converte o Value Object de domínio no formato persistido.</summary>
    public static UserAddress From(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new UserAddress
        {
            PostalCode = address.PostalCode.Value,
            Street = address.Street,
            Number = address.Number,
            Complement = address.Complement,
            District = address.District,
            City = address.City.Name,
            CityIbgeCode = address.City.IbgeCode,
            State = address.State.Value,
        };
    }
}
