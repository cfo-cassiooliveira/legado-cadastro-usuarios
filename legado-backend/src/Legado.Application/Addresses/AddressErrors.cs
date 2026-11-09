using Legado.Domain.Common;

namespace Legado.Application.Addresses;

/// <summary>Erros de endereço que dependem de consulta externa (não são invariantes de domínio).</summary>
internal static class AddressErrors
{
    /// <summary>CEP inexistente na base dos Correios (NotFound na consulta, Validation no cadastro).</summary>
    public static DomainError PostalCodeNotFound(ErrorType type) =>
        new("postal_code.not_found", "postalCode", "Postal code was not found.", type);

    /// <summary>O CEP pertence a outro município.</summary>
    public static DomainError PostalCodeCityMismatch() =>
        new("postal_code.city_mismatch", "postalCode", "Postal code does not belong to the selected city.");

    /// <summary>O município escolhido não pertence à UF informada.</summary>
    public static DomainError CityNotInState() =>
        new("city.not_in_state", "cityIbgeCode", "City does not belong to the selected state.");
}
