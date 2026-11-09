using Legado.Domain.Users;

namespace Legado.Domain.Tests.Users;

public sealed class FullNameTests
{
    [Theory]
    [InlineData("Ana Souza", "Ana Souza")]
    [InlineData("  João   Pedro  D'Ávila  ", "João Pedro D'Ávila")]
    [InlineData("Maria-Clara Lima", "Maria-Clara Lima")]
    public void Create_WithValidName_ReturnsNormalizedName(string input, string expected)
    {
        var result = FullName.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, "full_name.required")]
    [InlineData("   ", "full_name.required")]
    [InlineData("Ana", "full_name.surname_required")]
    [InlineData("Ana 123", "full_name.invalid_characters")]
    [InlineData("-Ana Souza", "full_name.invalid_characters")]
    public void Create_WithInvalidName_ReturnsError(string? input, string expectedCode)
    {
        var result = FullName.Create(input);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Create_WhenLongerThanMaxLength_ReturnsTooLong()
    {
        var input = "Ana " + new string('a', FullName.MaxLength);

        var result = FullName.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("full_name.too_long");
    }
}
