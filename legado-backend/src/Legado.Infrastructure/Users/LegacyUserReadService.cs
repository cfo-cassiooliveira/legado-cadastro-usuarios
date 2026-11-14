using Legado.Application.Users;
using Legado.Application.Users.Migration;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Users;

/// <summary>
/// Leituras das contas para a migração: login ou e-mail normalizados como o Identity grava, projeção direta e sem
/// tracking. Nunca lê o hash da senha.
/// </summary>
internal sealed class LegacyUserReadService(LegadoDbContext dbContext, ILookupNormalizer normalizer) : ILegacyUserReadService
{
    /// <summary>Procura pelo login e, se não achar, pelo e-mail.</summary>
    public async Task<string?> FindUserNameAsync(string login, CancellationToken cancellationToken) =>
        await ByLogin(login).Select(u => u.UserName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Busca a conta com endereço e perfis numa única consulta.</summary>
    public async Task<(LegacyUser User, bool IsMigrated)?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var row = await ByLogin(login)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.EmailConfirmed,
                u.FullName,
                u.PhoneNumber,
                u.Address,
                u.MigratedAt,
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
            row.Address.PostalCode, row.Address.Street, row.Address.Number, row.Address.Complement,
            row.Address.District, row.Address.City, row.Address.CityIbgeCode, row.Address.State);
        var user = new LegacyUser(
            row.Id,
            row.UserName ?? string.Empty,
            row.Email ?? string.Empty,
            row.EmailConfirmed,
            row.FullName,
            row.PhoneNumber ?? string.Empty,
            address,
            [.. row.Roles.OfType<string>().Order(StringComparer.Ordinal)]);
        return (user, row.MigratedAt is not null);
    }

    /// <summary>Login e e-mail em uso por contas não migradas (conta migrada já está no Keycloak, que barra sozinho).</summary>
    public async Task<LegacyUserExistence> CheckExistenceAsync(string userName, string email, CancellationToken cancellationToken)
    {
        var normalizedName = normalizer.NormalizeName(userName);
        var normalizedEmail = normalizer.NormalizeEmail(email);
        var active = dbContext.Users.AsNoTracking().Where(u => u.MigratedAt == null);
        var userNameTaken = await active.AnyAsync(u => u.NormalizedUserName == normalizedName, cancellationToken);
        var emailTaken = await active.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        return new LegacyUserExistence(userNameTaken, emailTaken);
    }

    private IQueryable<ApplicationUser> ByLogin(string login)
    {
        var normalizedName = normalizer.NormalizeName(login);
        var normalizedEmail = normalizer.NormalizeEmail(login);
        // Login tem prioridade sobre e-mail (o e-mail de uma conta nunca coincide com o login de outra: login não tem "@").
        return dbContext.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == normalizedName || u.NormalizedEmail == normalizedEmail)
            .OrderBy(u => u.NormalizedUserName == normalizedName ? 0 : 1);
    }
}
