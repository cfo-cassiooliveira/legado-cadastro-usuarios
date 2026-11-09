using Legado.Domain.Addresses;

namespace Legado.Domain.Tests.Addresses;

public sealed class StateCodeTests
{
    [Theory]
    [InlineData("sp", "SP")]
    [InlineData(" DF ", "DF")]
    public void Create_WithValidState_ReturnsUppercase(string input, string expected)
    {
        StateCode.Create(input).Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "state.required")]
    [InlineData("XX", "state.invalid")]
    [InlineData("São Paulo", "state.invalid")]
    public void Create_WithInvalidState_ReturnsError(string input, string expectedCode)
    {
        StateCode.Create(input).Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }
}
