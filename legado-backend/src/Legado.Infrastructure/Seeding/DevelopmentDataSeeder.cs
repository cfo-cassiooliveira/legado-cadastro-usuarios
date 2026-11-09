using Legado.Application.Users;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Legado.Infrastructure.Seeding;

/// <summary>Cria a massa de usuários de demonstração uma única vez (idempotente: só roda com a base vazia).</summary>
public sealed partial class DevelopmentDataSeeder(
    UserManager<ApplicationUser> userManager,
    LegadoDbContext dbContext,
    IOptions<SeedOptions> options,
    ILogger<DevelopmentDataSeeder> logger)
{
    /// <summary>Cria admin + usuários fictícios se o seed estiver habilitado e a base estiver vazia.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(settings.DefaultPassword))
        {
            throw new InvalidOperationException("Seed:DefaultPassword must be configured when seeding is enabled.");
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            LogSeedSkipped();
            return;
        }

        var users = DemoUsers.Create();
        foreach (var user in users)
        {
            await CreateAsync(user, settings.DefaultPassword);
        }

        LogSeedCompleted(users.Count);
    }

    private async Task CreateAsync(ApplicationUser user, string password)
    {
        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.User));

        if (user.UserName == DemoUsers.AdminUserName)
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.Admin));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        // Falha no seed é erro de configuração (ex.: senha fora da política); interrompe a inicialização.
        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            throw new InvalidOperationException($"Seeding failed: {codes}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data seed skipped: users already exist")]
    private partial void LogSeedSkipped();

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data seed created {UserCount} users")]
    private partial void LogSeedCompleted(int userCount);
}
