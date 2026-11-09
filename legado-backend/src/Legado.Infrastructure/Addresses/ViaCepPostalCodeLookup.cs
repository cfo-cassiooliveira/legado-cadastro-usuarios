using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Polly;

namespace Legado.Infrastructure.Addresses;

/// <summary>Adapter para o ViaCEP (base de CEPs dos Correios). Ver ADR-0002.</summary>
internal sealed class ViaCepPostalCodeLookup(HttpClient httpClient) : IPostalCodeLookup
{
    /// <summary>Consulta o CEP no ViaCEP; devolve <c>null</c> para CEP inexistente e lança indisponibilidade em falha de rede.</summary>
    public async Task<PostalCodeAddress?> FindAsync(PostalCode postalCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postalCode);

        try
        {
            // CEP já validado como 8 dígitos: seguro para compor a rota.
            using var response = await httpClient.GetAsync(new Uri($"ws/{postalCode.Value}/json/", UriKind.Relative), cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ViaCepResponse>(cancellationToken);

            // O ViaCEP responde 200 com {"erro": true} para CEP inexistente.
            if (payload is null || payload.Error is not null || !int.TryParse(payload.IbgeCode, out var ibgeCode))
            {
                return null;
            }

            return new PostalCodeAddress(
                postalCode.Value,
                payload.Street ?? string.Empty,
                payload.District ?? string.Empty,
                payload.City ?? string.Empty,
                ibgeCode,
                payload.State ?? string.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException or JsonException)
        {
            throw new AddressServiceUnavailableException("Postal code service is unavailable.", ex);
        }
    }

    private sealed record ViaCepResponse(
        [property: JsonPropertyName("logradouro")] string? Street,
        [property: JsonPropertyName("bairro")] string? District,
        [property: JsonPropertyName("localidade")] string? City,
        [property: JsonPropertyName("uf")] string? State,
        [property: JsonPropertyName("ibge")] string? IbgeCode,
        [property: JsonPropertyName("erro")] JsonElement? Error);
}
