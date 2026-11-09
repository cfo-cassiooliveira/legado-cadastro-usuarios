using Legado.Domain.Common;

namespace Legado.Application.Users;

/// <summary>Lista usuários paginados (uso administrativo).</summary>
public interface IListUsersHandler
{
    /// <summary>Valida a paginação (página ≥ 1, tamanho 1–100) e lista os usuários.</summary>
    Task<Result<PagedResult<UserSummary>>> HandleAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Implementação do caso de uso de listagem paginada de usuários.</summary>
internal sealed class ListUsersHandler(IUserReadService userReadService) : IListUsersHandler
{
    /// <summary>Tamanho máximo de página permitido na listagem.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Valida os parâmetros de paginação e delega a consulta ao serviço de leitura.</summary>
    public async Task<Result<PagedResult<UserSummary>>> HandleAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var errors = new List<DomainError>();
        if (page < 1)
        {
            errors.Add(new DomainError("page.invalid", "page", "Page must be greater than zero."));
        }

        // Limite evita listagens ilimitadas (paginação obrigatória).
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors.Add(new DomainError("page_size.invalid", "pageSize", "Page size must be between 1 and 100."));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<PagedResult<UserSummary>>(errors);
        }

        var result = await userReadService.ListAsync(page, pageSize, cancellationToken);
        return Result.Success(result);
    }
}
