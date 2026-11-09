using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Legado.Infrastructure.Addresses;

/// <summary>Decorator de cache para o catálogo de municípios (a lista do IBGE muda raramente).</summary>
internal sealed class CachedCityDirectory(
    ICityDirectory inner,
    IMemoryCache cache,
    IOptions<AddressServicesOptions> options) : ICityDirectory
{
    /// <summary>Devolve os municípios do cache ou consulta o IBGE e guarda o resultado.</summary>
    public async Task<IReadOnlyList<City>> ListByStateAsync(StateCode state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var cacheKey = $"cities:{state.Value}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<City>? cached) && cached is not null)
        {
            return cached;
        }

        var cities = await inner.ListByStateAsync(state, cancellationToken);
        // Lista vazia indica resposta anômala do serviço; não cacheamos para tentar de novo depois.
        if (cities.Count > 0)
        {
            cache.Set(cacheKey, cities, options.Value.CitiesCacheDuration);
        }

        return cities;
    }
}
