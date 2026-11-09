namespace Legado.Api.Auth;

/// <summary>
/// Cookie que guarda o fingerprint do token. <c>HttpOnly</c> (JavaScript não lê), <c>Secure</c>,
/// <c>SameSite=Strict</c> e prefixo <c>__Host-</c> (sem domínio, path "/"). Um token roubado sem este cookie é inútil.
/// </summary>
internal static class FingerprintCookie
{
    /// <summary>Nome do cookie (o prefixo <c>__Host-</c> obriga Secure, Path=/ e ausência de Domain).</summary>
    public const string Name = "__Host-legado-fgp";

    /// <summary>Grava o cookie com a mesma validade do token.</summary>
    public static void Append(HttpResponse response, string fingerprint, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(Name, fingerprint, BuildOptions(expiresAt));
    }

    /// <summary>Remove o cookie do navegador (logout).</summary>
    public static void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, BuildOptions(expiresAt: null));
    }

    /// <summary>Lê o fingerprint enviado pelo navegador; <c>null</c> se ausente.</summary>
    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies[Name];
    }

    private static CookieOptions BuildOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAt,
        IsEssential = true,
    };
}
