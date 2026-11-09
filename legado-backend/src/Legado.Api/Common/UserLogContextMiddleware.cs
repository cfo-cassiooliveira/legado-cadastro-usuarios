using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog.Context;

namespace Legado.Api.Common;

/// <summary>
/// Inclui o <c>user_id</c> (id técnico do token) em todos os logs emitidos durante a requisição autenticada,
/// inclusive os dos casos de uso. Deve vir depois de <c>UseAuthentication</c>.
/// </summary>
internal sealed class UserLogContextMiddleware(RequestDelegate next)
{
    /// <summary>Empilha a propriedade no LogContext do Serilog enquanto a requisição é processada.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty("user_id", userId))
        {
            await next(context);
        }
    }
}
