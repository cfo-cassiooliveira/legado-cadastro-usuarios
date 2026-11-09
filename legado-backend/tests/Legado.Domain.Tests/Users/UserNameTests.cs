using Legado.Domain.Users;

namespace Legado.Domain.Tests.Users;

public sealed class UserNameTests
{
    [Theory]
    [InlineData("ana.souza", "ana.souza")]
    [InlineData("  Ana_Souza-1 ", "ana_souza-1")]
    public void Create_WithValidUserName_ReturnsLowercaseValue(string input, string expected)
    {
        var result = UserName.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "user_name.required")]
    [InlineData("ana", "user_name.invalid_length")]
    [InlineData("a234567890123456789012345678901", "user_name.invalid_length")]
    [InlineData("ana souza", "user_name.invalid_characters")]
    [InlineData(".ana", "user_name.invalid_characters")]
    [InlineData("ana@souza", "user_name.invalid_characters")]
    public void Create_WithInvalidUserName_ReturnsError(string input, string expectedCode)
    {
        var result = UserName.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }
}
