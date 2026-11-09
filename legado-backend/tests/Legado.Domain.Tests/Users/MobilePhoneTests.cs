using Legado.Domain.Users;

namespace Legado.Domain.Tests.Users;

public sealed class MobilePhoneTests
{
    [Theory]
    [InlineData("(16) 99123-4567", "16991234567")]
    [InlineData("16991234567", "16991234567")]
    [InlineData("+55 11 98765-4321", "11987654321")]
    public void Create_WithValidMobile_ReturnsNationalNumber(string input, string expected)
    {
        var result = MobilePhone.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.NationalNumber.ShouldBe(expected);
        result.Value.E164.ShouldBe($"+55{expected}");
    }

    [Theory]
    [InlineData("", "mobile_phone.required")]
    [InlineData("1691234567", "mobile_phone.invalid")]
    [InlineData("16891234567", "mobile_phone.invalid")]
    [InlineData("10991234567", "mobile_phone.invalid_area_code")]
    [InlineData("20991234567", "mobile_phone.invalid_area_code")]
    public void Create_WithInvalidMobile_ReturnsError(string input, string expectedCode)
    {
        var result = MobilePhone.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }
}
