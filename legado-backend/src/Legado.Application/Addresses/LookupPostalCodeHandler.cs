using Legado.Domain.Addresses;
using Legado.Domain.Common;

namespace Legado.Application.Addresses;

/// <summary>Consulta um CEP para pré-preencher o endereço no cadastro.</summary>
public interface ILookupPostalCodeHandler
{
    /// <summary>Valida o formato do CEP e consulta o endereço; CEP inexistente retorna erro NotFound.</summary>
    Task<Result<PostalCodeAddress>> HandleAsync(string postalCode, CancellationToken cancellationToken);
}

/// <summary>Implementação do caso de uso de consulta de CEP.</summary>
internal sealed class LookupPostalCodeHandler(IPostalCodeLookup postalCodeLookup) : ILookupPostalCodeHandler
{
    /// <summary>Valida o CEP e consulta o endereço no serviço externo.</summary>
    public async Task<Result<PostalCodeAddress>> HandleAsync(string postalCode, CancellationToken cancellationToken)
    {
        var parsed = PostalCode.Create(postalCode);
        if (!parsed.IsSuccess)
        {
            return Result.Failure<PostalCodeAddress>(parsed.Errors);
        }

        var address = await postalCodeLookup.FindAsync(parsed.Value, cancellationToken);
        return address is null
            ? Result.Failure<PostalCodeAddress>(AddressErrors.PostalCodeNotFound(ErrorType.NotFound))
            : Result.Success(address);
    }
}
