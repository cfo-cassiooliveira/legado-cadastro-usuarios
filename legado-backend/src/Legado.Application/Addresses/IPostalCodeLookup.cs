using Legado.Domain.Addresses;

namespace Legado.Application.Addresses;

/// <summary>Porta para consulta de CEP em serviço externo.</summary>
public interface IPostalCodeLookup
{
    /// <summary>Retorna o endereço do CEP ou <c>null</c> quando o CEP não existe.</summary>
    /// <exception cref="AddressServiceUnavailableException">Serviço externo indisponível.</exception>
    Task<PostalCodeAddress?> FindAsync(PostalCode postalCode, CancellationToken cancellationToken);
}
