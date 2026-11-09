using System.Security.Claims;
using Legado.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Legado.Api.Auth;

/// <summary>
/// Validações extras após a assinatura/expiração do JWT: o token precisa vir acompanhado do cookie de fingerprint
/// e a sessão precisa continuar ativa no servidor (sem logout, sem bloqueio, usuário existente).
/// </summary>
internal sealed partial class LegacyJwtBearerEvents(
    ISessionValidator sessionValidator,
    ILogger<LegacyJwtBearerEvents> logger) : JwtBearerEvents
{
    /// <summary>Recusa tokens sem fingerprint correspondente ou com sessão revogada.</summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var principal = context.Principal;

        // Token roubado (XSS, log, DevTools) não funciona sem o cookie HttpOnly emitido junto no login.
        var fingerprint = FingerprintCookie.Read(context.Request);
        if (!TokenHash.Matches(fingerprint, principal?.FindFirstValue(JwtClaimTypes.FingerprintHash)))
        {
            LogRejected("fingerprint mismatch");
            context.Fail("Token fingerprint mismatch.");
            return;
        }

        if (!Guid.TryParse(principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            LogRejected("invalid subject");
            context.Fail("Invalid token subject.");
            return;
        }

        var stampHash = principal?.FindFirstValue(JwtClaimTypes.SecurityStampHash);
        if (!await sessionValidator.IsActiveAsync(userId, stampHash, context.HttpContext.RequestAborted))
        {
            LogRejected("session revoked or account locked");
            context.Fail("Session is no longer active.");
        }
    }

    // O motivo vai para o log (auditoria, OWASP A09); o token em si nunca é logado.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Access token rejected: {Reason}")]
    private partial void LogRejected(string reason);
}
