using Legado.Application.Users;
using Legado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Users;

/// <summary>Leituras de usuários com projeção direta e sem tracking.</summary>
internal sealed class UserReadService(LegadoDbContext dbContext) : IUserReadService
{
    /// <summary>Busca o usuário com endereço e perfis numa única consulta sem tracking.</summary>
    public async Task<UserDetails?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.Address,
                u.CreatedAt,
                Roles = dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var address = new UserAddressDto(
            row.Address.PostalCode,
            row.Address.Street,
            row.Address.Number,
            row.Address.Complement,
            row.Address.District,
            row.Address.City,
            row.Address.CityIbgeCode,
            row.Address.State);

        return new UserDetails(
            row.Id,
            row.UserName ?? string.Empty,
            row.FullName,
            row.Email ?? string.Empty,
            row.PhoneNumber ?? string.Empty,
            address,
            [.. row.Roles.OfType<string>().Order(StringComparer.Ordinal)],
            row.CreatedAt);
    }

    /// <summary>Conta o total e devolve a página solicitada ordenada por login.</summary>
    public async Task<PagedResult<UserSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        // Ordenação por chave única garante paginação estável.
        var items = await query
            .OrderBy(u => u.UserName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummary(u.Id, u.UserName ?? string.Empty, u.FullName, u.Email ?? string.Empty, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummary>(items, page, pageSize, totalCount);
    }
}
