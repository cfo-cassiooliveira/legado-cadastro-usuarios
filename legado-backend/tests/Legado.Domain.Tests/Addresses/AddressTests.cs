using Legado.Domain.Addresses;

namespace Legado.Domain.Tests.Addresses;

public sealed class AddressTests
{
    private static readonly PostalCode SamplePostalCode = PostalCode.Create("01001000").Value;
    private static readonly City SampleCity = new(3550308, "São Paulo");
    private static readonly StateCode SampleState = StateCode.Create("SP").Value;

    [Fact]
    public void Create_WithValidData_TrimsAndKeepsCity()
    {
        var result = Address.Create(SamplePostalCode, " Praça da Sé ", " 10 ", "  ", " Sé ", SampleCity, SampleState);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Street.ShouldBe("Praça da Sé");
        result.Value.Number.ShouldBe("10");
        result.Value.Complement.ShouldBeNull();
        result.Value.District.ShouldBe("Sé");
        result.Value.City.IbgeCode.ShouldBe(3550308);
    }

    [Fact]
    public void Create_WithMissingRequiredFields_ReturnsAllErrors()
    {
        var result = Address.Create(SamplePostalCode, "", null, null, " ", SampleCity, SampleState);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.Select(e => e.Code).ShouldBe(["street.required", "number.required", "district.required"], ignoreOrder: true);
    }

    [Fact]
    public void Create_WithTooLongComplement_ReturnsError()
    {
        var complement = new string('a', Address.ComplementMaxLength + 1);

        var result = Address.Create(SamplePostalCode, "Rua A", "1", complement, "Centro", SampleCity, SampleState);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("complement.too_long");
    }

    [Fact]
    public void Create_WithTooLongStreet_ReturnsError()
    {
        var street = new string('a', Address.StreetMaxLength + 1);

        var result = Address.Create(SamplePostalCode, street, "1", null, "Centro", SampleCity, SampleState);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("street.too_long");
    }
}
