using Legado.Domain.Addresses;

namespace Legado.Domain.Tests.Addresses;

public sealed class PostalCodeTests
{
    [Theory]
    [InlineData("01001-000", "01001000")]
    [InlineData("01.001-000", "01001000")]
    [InlineData(" 14010000 ", "14010000")]
    public void Create_WithValidPostalCode_ReturnsDigitsOnly(string input, string expected)
    {
        var result = PostalCode.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Fact]
    public void Formatted_ReturnsMaskedValue()
    {
        PostalCode.Create("01001000").Value.Formatted.ShouldBe("01001-000");
    }

    [Theory]
    [InlineData(null, "postal_code.required")]
    [InlineData("1234567", "postal_code.invalid")]
    [InlineData("123456789", "postal_code.invalid")]
    [InlineData("0100100A", "postal_code.invalid")]
    [InlineData("00000000", "postal_code.invalid")]
    public void Create_WithInvalidPostalCode_ReturnsError(string? input, string expectedCode)
    {
        var result = PostalCode.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }
}
