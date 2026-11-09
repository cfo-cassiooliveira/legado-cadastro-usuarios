using Legado.Domain.Addresses;
using Legado.Domain.Common;

namespace Legado.Application.Addresses;

/// <summary>Lista os municípios de uma UF para o seletor de cidade.</summary>
public interface IListCitiesHandler
{
    /// <summary>Valida a UF e devolve os municípios dela em ordem alfabética.</summary>
    Task<Result<IReadOnlyList<City>>> HandleAsync(string state, CancellationToken cancellationToken);
}

/// <summary>Implementação do caso de uso de listagem de municípios.</summary>
internal sealed class ListCitiesHandler(ICityDirectory cityDirectory) : IListCitiesHandler
{
    /// <summary>Valida a UF e consulta o catálogo de municípios.</summary>
    public async Task<Result<IReadOnlyList<City>>> HandleAsync(string state, CancellationToken cancellationToken)
    {
        var parsed = StateCode.Create(state);
        if (!parsed.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<City>>(parsed.Errors);
        }

        var cities = await cityDirectory.ListByStateAsync(parsed.Value, cancellationToken);
        return Result.Success(cities);
    }
}
