using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users;
using Legado.Infrastructure.Persistence;
using Legado.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Legado.Api.IntegrationTests.Seeding;

/// <summary>Massa de demonstração: criada uma única vez, com o admin correto e senha obrigatória.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class DevelopmentDataSeederTests(LegadoApiFactory factory)
{
    // Senha fictícia só para o banco descartável do teste.
    private const string SeedPassword = "Demo@Seed123";
    private const int ExpectedUsers = 11;

    [Fact]
    public async Task Seed_OnEmptyDatabase_CreatesDemoUsersWithAdmin()
    {
        var connectionString = await factory.CreateEmptyDatabaseAsync();
        using var host = StartWithSeed(connectionString, SeedPassword);

        await using var scope = host.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();

        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(ExpectedUsers);
        var admin = await userManager.FindByNameAsync(DemoUsers.AdminUserName) ?? throw new InvalidOperationException("Admin not seeded.");
        (await userManager.GetRolesAsync(admin)).ShouldBe([Roles.Admin, Roles.User], ignoreOrder: true);
        (await userManager.CheckPasswordAsync(admin, SeedPassword)).ShouldBeTrue();

        var regular = await userManager.FindByNameAsync("ana.souza") ?? throw new InvalidOperationException("User not seeded.");
        (await userManager.GetRolesAsync(regular)).ShouldBe([Roles.User]);
    }

    [Fact]
    public async Task Seed_RunningTwice_DoesNotDuplicateUsers()
    {
        var connectionString = await factory.CreateEmptyDatabaseAsync();
        using (StartWithSeed(connectionString, SeedPassword))
        {
        }

        using var secondStart = StartWithSeed(connectionString, SeedPassword);

        await using var scope = secondStart.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();
        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(ExpectedUsers);
    }

    [Fact]
    public async Task Seed_EnabledWithoutPassword_FailsStartup()
    {
        var connectionString = await factory.CreateEmptyDatabaseAsync();

        var exception = Should.Throw<InvalidOperationException>(() => StartWithSeed(connectionString, password: string.Empty));

        exception.Message.ShouldContain("Seed:DefaultPassword");
    }

    [Fact]
    public async Task Seed_Disabled_CreatesNothing()
    {
        var connectionString = await factory.CreateEmptyDatabaseAsync();
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:LegadoDb", connectionString));
        _ = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();
        (await db.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    /// <summary>Sobe a API apontando para o banco informado com o seed habilitado (o seed roda no startup).</summary>
    private WebApplicationFactory<Program> StartWithSeed(string connectionString, string password)
    {
        var host = factory.WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:LegadoDb", connectionString)
            .UseSetting("Seed:Enabled", "true")
            .UseSetting("Seed:DefaultPassword", password));
        try
        {
            _ = host.CreateClient();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }
}
