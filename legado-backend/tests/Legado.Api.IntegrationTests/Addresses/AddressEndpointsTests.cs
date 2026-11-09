using System.Net;
using System.Net.Http.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Addresses;
using Legado.Domain.Addresses;

namespace Legado.Api.IntegrationTests.Addresses;

[Collection(ApiTestGroup.Name)]
public sealed class AddressEndpointsTests(LegadoApiFactory factory)
{
    [Fact]
    public async Task LookupPostalCode_WhenFound_ReturnsAddressAnonymously()
    {
        var address = await factory.CreateClient().GetFromJsonAsync<PostalCodeAddress>(
            "/api/v1/addresses/postal-codes/01001-000", TestContext.Current.CancellationToken);

        address.ShouldNotBeNull();
        address.City.ShouldBe("São Paulo");
        address.CityIbgeCode.ShouldBe(FakePostalCodeLookup.SaoPauloIbgeCode);
    }

    [Theory]
    [InlineData("99999999", HttpStatusCode.NotFound)]
    [InlineData("123", HttpStatusCode.BadRequest)]
    public async Task LookupPostalCode_WithUnknownOrInvalidCode_ReturnsProblem(string postalCode, HttpStatusCode expected)
    {
        var response = await factory.CreateClient().GetAsync(
            $"/api/v1/addresses/postal-codes/{postalCode}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task LookupPostalCode_WhenServiceIsDown_ReturnsServiceUnavailable()
    {
        factory.PostalCodes.IsUnavailable = true;
        try
        {
            var response = await factory.CreateClient().GetAsync(
                "/api/v1/addresses/postal-codes/01001000", TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("address_service.unavailable");
        }
        finally
        {
            factory.PostalCodes.IsUnavailable = false;
        }
    }

    [Fact]
    public async Task ListCities_ForState_ReturnsCities()
    {
        var cities = await factory.CreateClient().GetFromJsonAsync<List<City>>(
            "/api/v1/addresses/states/sp/cities", TestContext.Current.CancellationToken);

        cities.ShouldNotBeNull();
        cities.ShouldContain(c => c.IbgeCode == FakePostalCodeLookup.SaoPauloIbgeCode);
    }

    [Fact]
    public async Task ListCities_WithInvalidState_ReturnsBadRequest()
    {
        var response = await factory.CreateClient().GetAsync(
            "/api/v1/addresses/states/XX/cities", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
