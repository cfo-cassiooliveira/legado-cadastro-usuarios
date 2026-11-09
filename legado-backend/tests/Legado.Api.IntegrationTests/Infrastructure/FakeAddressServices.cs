using Legado.Application.Addresses;
using Legado.Domain.Addresses;

namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>Substitui o ViaCEP nos testes: sem dependência de rede externa.</summary>
public sealed class FakePostalCodeLookup : IPostalCodeLookup
{
    public const int SaoPauloIbgeCode = 3550308;

    public bool IsUnavailable { get; set; }

    public Task<PostalCodeAddress?> FindAsync(PostalCode postalCode, CancellationToken cancellationToken)
    {
        if (IsUnavailable)
        {
            throw new AddressServiceUnavailableException("Fake postal code service is down.");
        }

        var address = postalCode.Value == "01001000"
            ? new PostalCodeAddress("01001000", "Praça da Sé", "Sé", "São Paulo", SaoPauloIbgeCode, "SP")
            : null;
        return Task.FromResult(address);
    }
}

/// <summary>Substitui a API do IBGE nos testes.</summary>
public sealed class FakeCityDirectory : ICityDirectory
{
    public Task<IReadOnlyList<City>> ListByStateAsync(StateCode state, CancellationToken cancellationToken)
    {
        IReadOnlyList<City> cities = state.Value == "SP"
            ? [new City(FakePostalCodeLookup.SaoPauloIbgeCode, "São Paulo"), new City(3509502, "Campinas")]
            : [];
        return Task.FromResult(cities);
    }
}
