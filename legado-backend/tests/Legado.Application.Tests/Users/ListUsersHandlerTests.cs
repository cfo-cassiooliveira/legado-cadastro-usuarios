using Legado.Application.Users;
using NSubstitute;

namespace Legado.Application.Tests.Users;

public sealed class ListUsersHandlerTests
{
    private readonly IUserReadService _userReadService = Substitute.For<IUserReadService>();
    private readonly ListUsersHandler _handler;

    public ListUsersHandlerTests() => _handler = new ListUsersHandler(_userReadService);

    [Fact]
    public async Task HandleAsync_WithValidPaging_ReturnsPage()
    {
        var page = new PagedResult<UserSummary>([], 2, 10, 15);
        _userReadService.ListAsync(2, 10, Arg.Any<CancellationToken>()).Returns(page);

        var result = await _handler.HandleAsync(2, 10, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Theory]
    [InlineData(0, 10, "page.invalid")]
    [InlineData(1, 0, "page_size.invalid")]
    [InlineData(1, ListUsersHandler.MaxPageSize + 1, "page_size.invalid")]
    public async Task HandleAsync_WithInvalidPaging_ReturnsValidationError(int page, int pageSize, string expectedCode)
    {
        var result = await _handler.HandleAsync(page, pageSize, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
        await _userReadService.DidNotReceiveWithAnyArgs().ListAsync(default, default, TestContext.Current.CancellationToken);
    }
}
