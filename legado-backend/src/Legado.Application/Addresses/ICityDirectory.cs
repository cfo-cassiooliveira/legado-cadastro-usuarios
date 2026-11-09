using Legado.Domain.Addresses;

namespace Legado.Application.Addresses;

/// <summary>Porta para o catálogo oficial de municípios por UF.</summary>
public interface ICityDirectory
{
    /// <exception cref="AddressServiceUnavailableException">Serviço externo indisponível.</exception>
    Task<IReadOnlyList<City>> ListByStateAsync(StateCode state, CancellationToken cancellationToken);
}
