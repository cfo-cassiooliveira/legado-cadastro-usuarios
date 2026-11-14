using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Legado.Api.Internal;

/// <summary>
/// Autentica as chamadas internas pela assinatura HMAC (ADR-0005): chave conhecida, horário dentro da janela, corpo
/// íntegro (comparação em tempo constante) e nonce nunca visto antes para a chave (anti-reenvio). Qualquer falha é 401 genérico.
/// </summary>
internal sealed partial class LegadoHmacAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<InternalApiOptions> internalApi,
    IMemoryCache replayCache,
    TimeProvider timeProvider) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <summary>Nome do esquema de autenticação.</summary>
    public const string SchemeName = "LegadoHmac";

    /// <summary>Claim com o sistema chamador (<c>keycloak</c> ou <c>cadastro</c>).</summary>
    public const string CallerClaim = "caller";

    // Corpos das chamadas internas são pequenos (login + senha, ids): acima disso nem calcula a assinatura.
    private const int MaxBodyBytes = 16 * 1024;
    private const string ReplayKeyPrefix = "hmac-nonce:";

    private readonly ILogger _log = loggerFactory.CreateLogger<LegadoHmacAuthenticationHandler>();

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HmacSignature.HeaderName, out var header))
        {
            return AuthenticateResult.NoResult();
        }

        if (HmacSignature.TryParse(header) is not { } parsed)
        {
            return Reject("malformed", keyId: null);
        }

        var keyId = parsed.KeyId;

        var settings = internalApi.Value;
        if (!settings.Keys.TryGetValue(keyId, out var key))
        {
            // Id desconhecido vem do chamador: não vai para o log (evita injeção e ruído).
            return Reject("unknown_key", keyId: null);
        }

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(now - parsed.Timestamp) > settings.SignatureWindow.TotalSeconds)
        {
            return Reject("expired", keyId);
        }

        if (Request.ContentLength > MaxBodyBytes)
        {
            return Reject("too_large", keyId);
        }

        // Sem Content-Length (corpo chunked) o teto acima não se aplica: a leitura limitada recusa o excesso.
        if (await ReadBodyAsync() is not { } body)
        {
            return Reject("too_large", keyId);
        }

        var canonical = HmacSignature.Canonical(parsed.Timestamp, parsed.Nonce, Request.Method, Request.Path + Request.QueryString, body);
        var expected = HmacSignature.Compute(Convert.FromBase64String(key.Secret), canonical);
        if (!CryptographicOperations.FixedTimeEquals(expected, parsed.Signature))
        {
            return Reject("invalid", keyId);
        }

        // Anti-reenvio: cada nonce vale uma vez por chave dentro da janela (cache local, uma instância). Usar o nonce,
        // e não a assinatura, deixa passar duas chamadas legítimas idênticas no mesmo segundo (retentativa do PUT).
        var replayKey = ReplayKeyPrefix + keyId + ":" + parsed.Nonce;
        if (replayCache.TryGetValue(replayKey, out _))
        {
            return Reject("replay", keyId);
        }

        replayCache.Set(replayKey, true, settings.SignatureWindow * 2);
        var identity = new ClaimsIdentity(
            [new Claim(CallerClaim, key.Caller), new Claim("key_id", keyId)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>Lê no máximo <see cref="MaxBodyBytes"/>; <c>null</c> se o corpo passar disso (nada além é lido).</summary>
    private async Task<byte[]?> ReadBodyAsync()
    {
        // O endpoint lê o corpo de novo depois: o buffer volta para o início.
        Request.EnableBuffering(MaxBodyBytes);
        var buffer = new byte[MaxBodyBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length
            && (read = await Request.Body.ReadAsync(buffer.AsMemory(total), Context.RequestAborted)) > 0)
        {
            total += read;
        }

        if (total > MaxBodyBytes)
        {
            return null;
        }

        Request.Body.Position = 0;
        return buffer[..total];
    }

    private AuthenticateResult Reject(string reason, string? keyId)
    {
        LogRejected(reason, keyId ?? "unknown");
        return AuthenticateResult.Fail("Invalid internal API signature.");
    }

    // Nunca a assinatura nem a chave no log; só o motivo e o id da chave conhecida.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Internal API signature rejected ({Reason}) for key {KeyId}")]
    private static partial void LogRejected(ILogger logger, string reason, string keyId);

    private void LogRejected(string reason, string keyId) => LogRejected(_log, reason, keyId);
}
