using Legado.Domain.Addresses;

namespace Legado.Api.Addresses;

/// <summary>Município no contrato da API (DTO; desacopla o JSON do Value Object de domínio).</summary>
internal sealed record CityResponse(int IbgeCode, string Name)
{
    /// <summary>Converte o município de domínio no DTO de resposta.</summary>
    public static CityResponse From(City city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return new CityResponse(city.IbgeCode, city.Name);
    }
}
