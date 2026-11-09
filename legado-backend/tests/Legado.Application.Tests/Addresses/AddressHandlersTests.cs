using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Legado.Domain.Common;
using NSubstitute;

namespace Legado.Application.Tests.Addresses;

public sealed class AddressHandlersTests
{
    private readonly IPostalCodeLookup _postalCodeLookup = Substitute.For<IPostalCodeLookup>();
    private readonly ICityDirectory _cityDirectory = Substitute.For<ICityDirectory>();

    [Fact]
    public async Task LookupPostalCode_WhenFound_ReturnsAddress()
    {
        var address = new PostalCodeAddress("01001000", "Praça da Sé", "Sé", "São Paulo", 3550308, "SP");
        _postalCodeLookup.FindAsync(Arg.Is<PostalCode>(p => p.Value == "01001000"), Arg.Any<CancellationToken>()).Returns(address);
        var handler = new LookupPostalCodeHandler(_postalCodeLookup);

        var result = await handler.HandleAsync("01001-000", TestContext.Current.CancellationToken);

        result.Value.ShouldBe(address);
    }

    [Fact]
    public async Task LookupPostalCode_WhenNotFound_ReturnsNotFoundError()
    {
        var handler = new LookupPostalCodeHandler(_postalCodeLookup);

        var result = await handler.HandleAsync("99999999", TestContext.Current.CancellationToken);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("postal_code.not_found");
        error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task LookupPostalCode_WithInvalidFormat_DoesNotCallService()
    {
        var handler = new LookupPostalCodeHandler(_postalCodeLookup);

        var result = await handler.HandleAsync("123", TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("postal_code.invalid");
        await _postalCodeLookup.DidNotReceiveWithAnyArgs().FindAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListCities_WithValidState_ReturnsCities()
    {
        IReadOnlyList<City> cities = [new City(3550308, "São Paulo")];
        _cityDirectory.ListByStateAsync(Arg.Is<StateCode>(s => s.Value == "SP"), Arg.Any<CancellationToken>()).Returns(cities);
        var handler = new ListCitiesHandler(_cityDirectory);

        var result = await handler.HandleAsync("sp", TestContext.Current.CancellationToken);

        result.Value.ShouldBe(cities);
    }

    [Fact]
    public async Task ListCities_WithInvalidState_ReturnsValidationError()
    {
        var handler = new ListCitiesHandler(_cityDirectory);

        var result = await handler.HandleAsync("XX", TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("state.invalid");
    }
}
