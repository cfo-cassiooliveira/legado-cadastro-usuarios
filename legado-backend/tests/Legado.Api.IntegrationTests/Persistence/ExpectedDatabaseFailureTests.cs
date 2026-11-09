using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users;
using Legado.Domain.Addresses;
using Legado.Domain.Common;
using Legado.Domain.Users;
using Legado.Infrastructure.Auth;
using Legado.Infrastructure.Persistence;
using Legado.Infrastructure.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Legado.Api.IntegrationTests.Persistence;

/// <summary>
/// Falhas previstas de banco (unicidade no insert, concorrência no update) viram Warning + resultado tratado,
/// nunca exceção 500. Falhas imprevistas continuam Error (ver <c>LogLevelPolicyTests</c>).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ExpectedDatabaseFailureTests(LegadoApiFactory factory)
{
    [Fact]
    public async Task Insert_ViolatingUniqueEmailIndex_ReturnsConflictAndLogsWarningWithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var marker = Guid.NewGuid().ToString("N")[..8];
        var email = $"corrida.{marker}@exemplo.com";
        await TestUsers.RegisterAsync(factory.CreateClient(), ct); // garante base com usuários
        (await factory.CreateClient().PostAsync("/api/v1/users",
            System.Net.Http.Json.JsonContent.Create(TestUsers.NewRegistration($"first.{marker}", email)), ct)).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Simula a corrida: sem os validadores do Identity, só o índice único do banco impede a duplicidade.
        userManager.UserValidators.Clear();
        var logger = new FakeLogger<UserAccountWriter>();
        var writer = new UserAccountWriter(userManager, scope.ServiceProvider.GetRequiredService<LegadoDbContext>(), logger);

        var result = await writer.CreateAsync(NewAccount($"second.{marker}", email), TestUsers.Password, ct);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("email.taken");
        error.Type.ShouldBe(ErrorType.Conflict);
        logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        logger.LatestRecord.Message.ShouldContain("EmailIndex");
        logger.LatestRecord.Message.ShouldNotContain(email, Case.Insensitive);
        logger.LatestRecord.Exception.ShouldBeNull();
    }

    // Regressão: a 2ª tentativa reutilizava a entidade rastreada (ConcurrencyStamp antigo) e falhava de novo.
    [Fact]
    public async Task RevokeSessions_WithRealConcurrentUpdate_ReloadsAndSucceedsOnRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var userName = await TestUsers.RegisterAsync(factory.CreateClient(), ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();
        var user = await userManager.FindByNameAsync(userName) ?? throw new InvalidOperationException("User not found.");
        var stampBefore = user.SecurityStamp;
        // Outro request altera o usuário depois que ele já foi carregado neste contexto.
        await db.Users.Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ConcurrencyStamp, Guid.NewGuid().ToString()), ct);
        var logger = new FakeLogger<UserSessionStore>();
        var store = new UserSessionStore(db, userManager, logger);

        var result = await store.RevokeAllAsync(user.Id, ct);

        result.IsSuccess.ShouldBeTrue();
        logger.Collector.Count.ShouldBe(1); // a 1ª tentativa conflitou (Warning) e a 2ª, com dados recarregados, gravou
        var stampAfter = await db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.SecurityStamp).SingleAsync(ct);
        stampAfter.ShouldNotBe(stampBefore);
    }

    [Fact]
    public async Task RevokeSessions_WhenConcurrencyFailurePersists_ReturnsConflictAndLogsWarning()
    {
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = "concorrente" };
        var userManager = SubstituteUserManager();
        userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        var logger = new FakeLogger<UserSessionStore>();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = new UserSessionStore(scope.ServiceProvider.GetRequiredService<LegadoDbContext>(), userManager, logger);

        var result = await store.RevokeAllAsync(user.Id, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("session.revoke_conflict");
        await userManager.Received(2).UpdateSecurityStampAsync(user);
        logger.Collector.GetSnapshot().ShouldAllBe(r => r.Level == LogLevel.Warning);
        logger.Collector.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RevokeSessions_WhenRetrySucceeds_ReturnsSuccess()
    {
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = "concorrente" };
        var userManager = SubstituteUserManager();
        userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        userManager.UpdateSecurityStampAsync(user).Returns(
            IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()), IdentityResult.Success);
        var logger = new FakeLogger<UserSessionStore>();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = new UserSessionStore(scope.ServiceProvider.GetRequiredService<LegadoDbContext>(), userManager, logger);

        var result = await store.RevokeAllAsync(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        logger.Collector.Count.ShouldBe(1);
        logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task FailedAttemptBookkeeping_WhenUpdateRejected_LogsWarningAndKeepsVerdict()
    {
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = "concorrente" };
        var userManager = SubstituteUserManager();
        userManager.FindByNameAsync("concorrente").Returns(user);
        userManager.IsLockedOutAsync(user).Returns(false);
        userManager.CheckPasswordAsync(user, "Errada@123").Returns(false);
        userManager.AccessFailedAsync(user).Returns(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        var logger = new FakeLogger<CredentialVerifier>();
        var verifier = new CredentialVerifier(userManager, logger);

        var verification = await verifier.VerifyAsync("concorrente", "Errada@123", TestContext.Current.CancellationToken);

        verification.Status.ShouldBe(Legado.Application.Auth.CredentialStatus.Invalid);
        logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        logger.LatestRecord.Message.ShouldContain("ConcurrencyFailure");
    }

    private static UserManager<ApplicationUser> SubstituteUserManager() =>
        Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

    private static NewUserAccount NewAccount(string userName, string email) => new(
        UserName.Create(userName).Value,
        FullName.Create("Ana Paula Souza").Value,
        EmailAddress.Create(email).Value,
        MobilePhone.Create("16991234567").Value,
        Address.Create(PostalCode.Create("01001000").Value, "Praça da Sé", "1", null, "Sé",
            new City(FakePostalCodeLookup.SaoPauloIbgeCode, "São Paulo"), StateCode.Create("SP").Value).Value);
}
