using Legado.Domain.Users;

namespace Legado.Domain.Tests.Users;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsLowercaseTrimmedValue()
    {
        var result = EmailAddress.Create("  Ana.Souza@Exemplo.COM.br ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("ana.souza@exemplo.com.br");
    }

    [Theory]
    [InlineData(null, "email.required")]
    [InlineData("ana", "email.invalid")]
    [InlineData("ana@exemplo", "email.invalid")]
    [InlineData("ana souza@exemplo.com", "email.invalid")]
    public void Create_WithInvalidEmail_ReturnsError(string? input, string expectedCode)
    {
        var result = EmailAddress.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Create_WhenLongerThanMaxLength_ReturnsInvalid()
    {
        var input = new string('a', EmailAddress.MaxLength) + "@exemplo.com";

        var result = EmailAddress.Create(input);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("email.invalid");
    }
}
