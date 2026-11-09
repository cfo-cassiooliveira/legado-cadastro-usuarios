namespace Legado.Application.Users;

/// <summary>Porta de leitura de usuários (lado query).</summary>
public interface IUserReadService
{
    /// <summary>Busca os dados completos de um usuário; <c>null</c> se não existir.</summary>
    Task<UserDetails?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Lista usuários paginados, ordenados por login.</summary>
    Task<PagedResult<UserSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
