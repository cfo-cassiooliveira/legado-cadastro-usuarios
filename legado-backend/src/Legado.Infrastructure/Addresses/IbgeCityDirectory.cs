using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Polly;

namespace Legado.Infrastructure.Addresses;

/// <summary>Adapter para a API de Localidades do IBGE (municípios por UF). Ver ADR-0002.</summary>
internal sealed class IbgeCityDirectory(HttpClient httpClient) : ICityDirectory
{
    /// <summary>Consulta os municípios da UF na API do IBGE, ordenados por nome.</summary>
    public async Task<IReadOnlyList<City>> ListByStateAsync(StateCode state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            // UF já validada contra a lista fixa: seguro para compor a rota.
            var uri = new Uri($"api/v1/localidades/estados/{state.Value}/municipios?orderBy=nome", UriKind.Relative);
            var payload = await httpClient.GetFromJsonAsync<List<IbgeCityResponse>>(uri, cancellationToken) ?? [];
            return [.. payload.Select(c => new City(c.Id, c.Name))];
        }
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException or JsonException)
        {
            throw new AddressServiceUnavailableException("City directory service is unavailable.", ex);
        }
    }

    private sealed record IbgeCityResponse(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("nome")] string Name);
}
