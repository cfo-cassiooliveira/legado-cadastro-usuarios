using Legado.Application.Users;
using Legado.Domain.Common;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Users;

/// <summary>Cria contas via <see cref="UserManager{TUser}"/> (hash PBKDF2 do Identity) com o perfil padrão.</summary>
internal sealed class UserAccountWriter(UserManager<ApplicationUser> userManager, LegadoDbContext dbContext) : IUserAccountWriter
{
    /// <summary>Cria o usuário (hash PBKDF2) e atribui o perfil User numa única transação.</summary>
    public async Task<Result<Guid>> CreateAsync(NewUserAccount account, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(password);

        // Usuário + perfil na mesma transação; a execution strategy exige que a transação fique dentro dela.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var user = CreateUser(account);

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                return Result.Failure<Guid>(IdentityErrorMapper.Map(created.Errors));
            }

            var roleAssigned = await userManager.AddToRoleAsync(user, Roles.User);
            if (!roleAssigned.Succeeded)
            {
                throw new InvalidOperationException("Default role could not be assigned to the new user.");
            }

            await transaction.CommitAsync(ct);
            return Result.Success(user.Id);
        }, cancellationToken);
    }

    private static ApplicationUser CreateUser(NewUserAccount account) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = account.UserName.Value,
        Email = account.Email.Value,
        PhoneNumber = account.MobilePhone.E164,
        FullName = account.FullName.Value,
        Address = UserAddress.From(account.Address),
    };
}
