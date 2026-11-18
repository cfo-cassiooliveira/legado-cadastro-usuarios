using Microsoft.AspNetCore.Routing;

namespace Legado.Api.Common;

/// <summary>
/// Devolve o caminho da requisição seguro para log (LGPD, Art. IV.6 da Constituição): nunca o valor cru da rota, que
/// pode carregar e-mail, login ou CPF digitados na URL (ex.: <c>/api/v1/users/{e-mail}</c>).
/// </summary>
internal static class RequestPathSanitizer
{
    /// <summary>Quantidade de segmentos preservados quando nenhuma rota casou (<c>/api/v1/users</c>).</summary>
    private const int UnmatchedSegmentsKept = 3;

    private const string UnmatchedSuffix = "/*";

    /// <summary>
    /// Rota casada: o padrão (<c>/api/v1/internal/users/{legacyUserId:guid}/migration</c>), sem valores. Sem rota
    /// (404): só os primeiros segmentos mais <c>/*</c>, pois o restante é texto livre do cliente.
    /// </summary>
    public static string Sanitize(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } pattern })
        {
            return pattern;
        }

        var segments = (context.Request.Path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "/";
        }

        var kept = "/" + string.Join('/', segments.Take(UnmatchedSegmentsKept));
        return segments.Length > UnmatchedSegmentsKept ? kept + UnmatchedSuffix : kept;
    }
}
