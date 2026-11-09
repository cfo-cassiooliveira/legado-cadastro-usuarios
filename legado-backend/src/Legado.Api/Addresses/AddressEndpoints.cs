using Legado.Api.Common;
using Legado.Application.Addresses;

namespace Legado.Api.Addresses;

/// <summary>Endpoints de apoio ao cadastro: consulta de CEP e lista de municípios por UF.</summary>
internal static class AddressEndpoints
{
    /// <summary>Mapeia <c>/api/v1/addresses</c> (anônimo, com rate limit).</summary>
    public static IEndpointRouteBuilder MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        // Anônimos por requisito: usados na tela de autocadastro, antes de existir login. Protegidos por rate limit.
        var group = app.MapGroup("/api/v1/addresses")
            .WithTags("Addresses")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.AddressLookup);

        group.MapGet("/postal-codes/{postalCode}", LookupPostalCodeAsync)
            .Produces<PostalCodeAddress>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/states/{state}/cities", ListCitiesAsync)
            .Produces<IReadOnlyList<CityResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> LookupPostalCodeAsync(string postalCode, ILookupPostalCodeHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(postalCode, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> ListCitiesAsync(string state, IListCitiesHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(state, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value.Select(CityResponse.From).ToList())
            : result.ToProblem();
    }
}
