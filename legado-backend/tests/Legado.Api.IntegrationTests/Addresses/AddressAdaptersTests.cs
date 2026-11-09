using System.Net;
using System.Text;
using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Legado.Infrastructure.Addresses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Legado.Api.IntegrationTests.Addresses;

/// <summary>Testa a tradução das respostas do ViaCEP/IBGE sem rede (HttpMessageHandler fake).</summary>
public sealed class AddressAdaptersTests
{
    private static readonly PostalCode Se = PostalCode.Create("01001000").Value;
    private static readonly StateCode Sp = StateCode.Create("SP").Value;

    [Fact]
    public async Task ViaCep_WhenFound_MapsAddress()
    {
        const string json = """{"cep":"01001-000","logradouro":"Praça da Sé","bairro":"Sé","localidade":"São Paulo","uf":"SP","ibge":"3550308"}""";
        var lookup = new ViaCepPostalCodeLookup(Client(HttpStatusCode.OK, json));

        var address = await lookup.FindAsync(Se, TestContext.Current.CancellationToken);

        address.ShouldBe(new PostalCodeAddress("01001000", "Praça da Sé", "Sé", "São Paulo", 3550308, "SP"));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"erro": true}""")]
    [InlineData(HttpStatusCode.OK, """{"erro": "true"}""")]
    [InlineData(HttpStatusCode.BadRequest, "")]
    public async Task ViaCep_WhenNotFound_ReturnsNull(HttpStatusCode status, string body)
    {
        var lookup = new ViaCepPostalCodeLookup(Client(status, body));

        (await lookup.FindAsync(Se, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    public async Task ViaCep_WhenServiceFails_ThrowsUnavailable(HttpStatusCode status, string body)
    {
        var lookup = new ViaCepPostalCodeLookup(Client(status, body));

        await Should.ThrowAsync<AddressServiceUnavailableException>(() => lookup.FindAsync(Se, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ibge_MapsCities()
    {
        const string json = """[{"id":3550308,"nome":"São Paulo","microrregiao":{}},{"id":3509502,"nome":"Campinas"}]""";
        var directory = new IbgeCityDirectory(Client(HttpStatusCode.OK, json));

        var cities = await directory.ListByStateAsync(Sp, TestContext.Current.CancellationToken);

        cities.ShouldBe([new City(3550308, "São Paulo"), new City(3509502, "Campinas")]);
    }

    [Fact]
    public async Task Ibge_WhenServiceFails_ThrowsUnavailable()
    {
        var directory = new IbgeCityDirectory(Client(HttpStatusCode.BadGateway, ""));

        await Should.ThrowAsync<AddressServiceUnavailableException>(() => directory.ListByStateAsync(Sp, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CachedDirectory_CallsInnerOnlyOnce()
    {
        var inner = new CountingCityDirectory();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AddressServicesOptions
        {
            ViaCepBaseUrl = new Uri("https://viacep.test/"),
            IbgeBaseUrl = new Uri("https://ibge.test/"),
        });
        var cached = new CachedCityDirectory(inner, cache, options);

        await cached.ListByStateAsync(Sp, TestContext.Current.CancellationToken);
        var second = await cached.ListByStateAsync(Sp, TestContext.Current.CancellationToken);

        inner.Calls.ShouldBe(1);
        second.ShouldHaveSingleItem();
    }

    private static HttpClient Client(HttpStatusCode status, string body) =>
        new(new StubHandler(status, body)) { BaseAddress = new Uri("https://service.test/") };

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class CountingCityDirectory : ICityDirectory
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<City>> ListByStateAsync(StateCode state, CancellationToken cancellationToken)
        {
            Calls++;
            IReadOnlyList<City> cities = [new City(3550308, "São Paulo")];
            return Task.FromResult(cities);
        }
    }
}
