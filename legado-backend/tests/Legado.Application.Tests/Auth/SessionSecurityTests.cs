using Legado.Application.Auth;
using Legado.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Legado.Application.Tests.Auth;

public sealed class SessionSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly IUserSessionStore _store = Substitute.For<IUserSessionStore>();
    private readonly SessionValidator _validator;

    public SessionSecurityTests() => _validator = new SessionValidator(_store, new FakeTimeProvider(Now));

    [Fact]
    public async Task IsActive_WhenStampMatchesAndNotLocked_ReturnsTrue()
    {
        _store.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(new SessionState("stamp-1", LockoutEnd: null));

        var active = await _validator.IsActiveAsync(_userId, TokenHash.Sha256Hex("stamp-1"), TestContext.Current.CancellationToken);

        active.ShouldBeTrue();
    }

    [Fact]
    public async Task IsActive_AfterLogoutChangedTheStamp_ReturnsFalse()
    {
        _store.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(new SessionState("stamp-2", LockoutEnd: null));

        var active = await _validator.IsActiveAsync(_userId, TokenHash.Sha256Hex("stamp-1"), TestContext.Current.CancellationToken);

        active.ShouldBeFalse();
    }

    [Fact]
    public async Task IsActive_WhenAccountIsLocked_ReturnsFalse()
    {
        _store.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(new SessionState("stamp-1", Now.AddMinutes(5)));

        var active = await _validator.IsActiveAsync(_userId, TokenHash.Sha256Hex("stamp-1"), TestContext.Current.CancellationToken);

        active.ShouldBeFalse();
    }

    [Fact]
    public async Task IsActive_WhenLockoutAlreadyExpired_ReturnsTrue()
    {
        _store.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(new SessionState("stamp-1", Now.AddMinutes(-1)));

        var active = await _validator.IsActiveAsync(_userId, TokenHash.Sha256Hex("stamp-1"), TestContext.Current.CancellationToken);

        active.ShouldBeTrue();
    }

    [Fact]
    public async Task IsActive_WhenUserNoLongerExists_ReturnsFalse()
    {
        _store.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns((SessionState?)null);

        var active = await _validator.IsActiveAsync(_userId, TokenHash.Sha256Hex("stamp-1"), TestContext.Current.CancellationToken);

        active.ShouldBeFalse();
    }

    [Fact]
    public async Task IsActive_WithoutStampClaim_ReturnsFalseWithoutQuerying()
    {
        var active = await _validator.IsActiveAsync(_userId, null, TestContext.Current.CancellationToken);

        active.ShouldBeFalse();
        await _store.DidNotReceiveWithAnyArgs().GetAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Logout_WhenConcurrentUpdatePersists_ReturnsConflictAndLogsWarning()
    {
        var logger = new FakeLogger<LogoutHandler>();
        var conflict = new DomainError("session.revoke_conflict", "session", "conflict", ErrorType.Conflict);
        _store.RevokeAllAsync(_userId, Arg.Any<CancellationToken>()).Returns(Result.Failure(conflict));
        var handler = new LogoutHandler(_store, logger);

        var result = await handler.HandleAsync(_userId, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Type.ShouldBe(ErrorType.Conflict);
        logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task Logout_RevokesAllTokensOfTheUserAndLogsInformation()
    {
        var logger = new FakeLogger<LogoutHandler>();
        _store.RevokeAllAsync(_userId, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new LogoutHandler(_store, logger);

        var result = await handler.HandleAsync(_userId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
        await _store.Received(1).RevokeAllAsync(_userId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("valor", true)]
    [InlineData("outro", false)]
    [InlineData(null, false)]
    public void TokenHash_Matches_ComparesSha256(string? value, bool expected)
    {
        TokenHash.Matches(value, TokenHash.Sha256Hex("valor")).ShouldBe(expected);
    }

    [Fact]
    public void TokenHash_Sha256Hex_IsDeterministicLowercaseHex()
    {
        TokenHash.Sha256Hex("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }
}
