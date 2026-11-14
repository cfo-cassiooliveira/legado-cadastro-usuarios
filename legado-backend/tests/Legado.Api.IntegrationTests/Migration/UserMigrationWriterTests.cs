using System.Net;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users.Migration;
using Legado.Domain.Common;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Legado.Api.IntegrationTests.Migration;

/// <summary>Marcação de conta migrada para o Keycloak (ADR-0005, RN-07/RN-08 da spec 001 do cadastro) no SQL Server real.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class UserMigrationWriterTests(LegadoApiFactory factory)
{
    [Fact]
    public async Task MarkMigrated_StoresKeycloakUserAndRenewsSecurityStamp()
    {
        var legacyUser = await RegisterAsync();
        var keycloakUserId = Guid.NewGuid();

        var result = await MarkAsync(legacyUser.Id, keycloakUserId);

        result.IsSuccess.ShouldBeTrue();
        var stored = await FindAsync(legacyUser.Id);
        stored.KeycloakUserId.ShouldBe(keycloakUserId);
        stored.MigratedAt.ShouldNotBeNull();
        stored.SecurityStamp.ShouldNotBe(legacyUser.SecurityStamp);
    }

    [Fact]
    public async Task MarkMigrated_WhenRepeatedForTheSameKeycloakUser_IsIdempotent()
    {
        var legacyUser = await RegisterAsync();
        var keycloakUserId = Guid.NewGuid();
        await MarkAsync(legacyUser.Id, keycloakUserId);
        var afterFirst = await FindAsync(legacyUser.Id);

        var result = await MarkAsync(legacyUser.Id, keycloakUserId);

        result.IsSuccess.ShouldBeTrue();
        var afterSecond = await FindAsync(legacyUser.Id);
        afterSecond.MigratedAt.ShouldBe(afterFirst.MigratedAt);
        afterSecond.SecurityStamp.ShouldBe(afterFirst.SecurityStamp);
    }

    [Fact]
    public async Task MarkMigrated_WhenAlreadyMigratedToAnotherKeycloakUser_IsConflict()
    {
        var legacyUser = await RegisterAsync();
        await MarkAsync(legacyUser.Id, Guid.NewGuid());

        var result = await MarkAsync(legacyUser.Id, Guid.NewGuid());

        result.Errors.ShouldHaveSingleItem().ShouldBe(UserMigrationErrors.AlreadyMigratedToAnotherUser);
    }

    [Fact]
    public async Task MarkMigrated_WhenKeycloakUserIsLinkedToAnotherLegacyUser_IsConflict()
    {
        var first = await RegisterAsync();
        var second = await RegisterAsync();
        var keycloakUserId = Guid.NewGuid();
        await MarkAsync(first.Id, keycloakUserId);

        var result = await MarkAsync(second.Id, keycloakUserId);

        result.Errors.ShouldHaveSingleItem().Type.ShouldBe(ErrorType.Conflict);
        (await FindAsync(second.Id)).MigratedAt.ShouldBeNull();
    }

    [Fact]
    public async Task MarkMigrated_WhenLegacyUserDoesNotExist_IsNotFound()
    {
        var result = await MarkAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Errors.ShouldHaveSingleItem().ShouldBe(UserMigrationErrors.NotFound);
    }

    // RN-08: a marcação encerra na hora as sessões que estavam abertas no Legado.
    [Fact]
    public async Task MarkMigrated_RevokesTheOpenLegacySessions()
    {
        using var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        var session = await TestUsers.LoginAsync(client, userName, TestContext.Current.CancellationToken);
        client.Authenticate(session);
        (await client.GetAsync("/api/v1/users/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var legacyUser = await FindByNameAsync(userName);

        await MarkAsync(legacyUser.Id, Guid.NewGuid());

        (await client.GetAsync("/api/v1/users/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<ApplicationUser> RegisterAsync()
    {
        using var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        return await FindByNameAsync(userName);
    }

    private async Task<Result> MarkAsync(Guid legacyUserId, Guid keycloakUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserMigrationWriter>()
            .MarkMigratedAsync(legacyUserId, keycloakUserId, TestContext.Current.CancellationToken);
    }

    private async Task<ApplicationUser> FindAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!;
    }

    private async Task<ApplicationUser> FindByNameAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName))!;
    }
}
