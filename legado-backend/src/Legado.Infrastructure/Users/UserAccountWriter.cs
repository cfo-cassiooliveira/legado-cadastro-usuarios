using Legado.Application.Users;
using Legado.Domain.Common;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Legado.Infrastructure.Users;

/// <summary>Cria contas via <see cref="UserManager{TUser}"/> (hash PBKDF2 do Identity) com o perfil padrão.</summary>
internal sealed partial class UserAccountWriter(
    UserManager<ApplicationUser> userManager,
    LegadoDbContext dbContext,
    ILogger<UserAccountWriter> logger) : IUserAccountWriter
{
    private const string UserNameIndex = "UserNameIndex";
    private const string EmailIndex = "EmailIndex";

    /// <summary>
    /// Cria o usuário (hash PBKDF2) e atribui o perfil User numa única transação. Violação de índice único no
    /// insert (corrida entre dois cadastros iguais) é falha prevista: vira conflito (409) e Warning, não erro 500.
    /// </summary>
    public async Task<Result<Guid>> CreateAsync(NewUserAccount account, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(password);

        try
        {
            return await CreateInTransactionAsync(account, password, cancellationToken);
        }
        catch (DbUpdateException ex) when (DatabaseErrors.IsUniqueViolation(ex, out var indexName, out var sqlErrorNumber))
        {
            // Sem a exceção no log: a mensagem do SQL Server contém o valor duplicado (dado pessoal).
            LogUniqueViolation(indexName, sqlErrorNumber);
            return Result.Failure<Guid>(ConflictFor(indexName));
        }
    }

    private async Task<Result<Guid>> CreateInTransactionAsync(NewUserAccount account, string password, CancellationToken cancellationToken)
    {
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

    private static DomainError ConflictFor(string indexName) => indexName switch
    {
        UserNameIndex => new DomainError("user_name.taken", "userName", "User name is already taken.", ErrorType.Conflict),
        EmailIndex => new DomainError("email.taken", "email", "Email is already registered.", ErrorType.Conflict),
        _ => new DomainError("account.duplicate", "account", "Account data conflicts with an existing account.", ErrorType.Conflict),
    };

    private static ApplicationUser CreateUser(NewUserAccount account) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = account.UserName.Value,
        Email = account.Email.Value,
        PhoneNumber = account.MobilePhone.E164,
        FullName = account.FullName.Value,
        Address = UserAddress.From(account.Address),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Insert rejected by unique index {IndexName} (SQL error {SqlErrorNumber})")]
    private partial void LogUniqueViolation(string indexName, int sqlErrorNumber);
}
