using Legado.Application.Auth;
using Legado.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Legado.Application.Tests.Auth;

public sealed class LoginHandlerTests
{
    private readonly ICredentialVerifier _credentialVerifier = Substitute.For<ICredentialVerifier>();
    private readonly ITokenIssuer _tokenIssuer = Substitute.For<ITokenIssuer>();
    private readonly LoginHandler _handler;

    public LoginHandlerTests() =>
        _handler = new LoginHandler(_credentialVerifier, _tokenIssuer, NullLogger<LoginHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsIssuedToken()
    {
        var user = new AuthenticatedUser(Guid.CreateVersion7(), "ana.souza", ["User"], "stamp");
        var expiresAt = new DateTimeOffset(2026, 11, 8, 12, 15, 0, TimeSpan.Zero);
        var issued = new AccessToken("jwt-token", expiresAt, "fingerprint");
        _credentialVerifier.VerifyAsync("ana.souza", "Senha@123", Arg.Any<CancellationToken>())
            .Returns(new CredentialVerification(CredentialStatus.Valid, user, user.Id));
        _tokenIssuer.Issue(user).Returns(issued);

        var result = await _handler.HandleAsync(new LoginCommand(" ana.souza ", "Senha@123"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(issued);
    }

    [Theory]
    [InlineData(CredentialStatus.Invalid)]
    [InlineData(CredentialStatus.LockedOut)]
    public async Task HandleAsync_WhenCredentialsAreRejected_ReturnsSameGenericError(CredentialStatus status)
    {
        _credentialVerifier.VerifyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialVerification(status, UserId: Guid.CreateVersion7()));

        var result = await _handler.HandleAsync(new LoginCommand("ana.souza", "errada"), TestContext.Current.CancellationToken);

        // Bloqueio e senha errada não podem ser distinguíveis pelo cliente (evita enumeração de contas).
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("auth.invalid_credentials");
        error.Type.ShouldBe(ErrorType.Unauthorized);
        _tokenIssuer.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Theory]
    [InlineData(null, "Senha@123")]
    [InlineData("ana.souza", null)]
    [InlineData(" ", "Senha@123")]
    public async Task HandleAsync_WithMissingCredentials_FailsWithoutVerifying(string? userName, string? password)
    {
        var result = await _handler.HandleAsync(new LoginCommand(userName, password), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("auth.invalid_credentials");
        await _credentialVerifier.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default!, TestContext.Current.CancellationToken);
    }
}
