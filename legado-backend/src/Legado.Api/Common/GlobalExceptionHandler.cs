using Legado.Application.Addresses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Legado.Api.Common;

/// <summary>Ponto único de tratamento e log de exceções não esperadas (Art. VI.3), sem stack trace na resposta.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Converte a exceção em Problem Details: 400 (requisição malformada), 503 (serviço de endereço fora) ou 500.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (exception is BadHttpRequestException badRequest)
        {
            // Requisição malformada (ex.: JSON/UTF-8 inválido): responde 400, mas é exceção capturada → Error (regra de log).
            LogBadRequest(badRequest, badRequest.StatusCode);
            problem = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "The request is malformed.",
            };
        }
        else if (exception is AddressServiceUnavailableException)
        {
            LogDependencyUnavailable(exception);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Address service is temporarily unavailable.",
                Extensions = { ["code"] = "address_service.unavailable" },
            };
        }
        else
        {
            LogUnhandled(exception);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            };
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    // Regra de log do workspace: toda exceção capturada é registrada como Error, com a exceção completa.
    [LoggerMessage(Level = LogLevel.Error, Message = "Malformed request rejected with status {StatusCode}")]
    private partial void LogBadRequest(Exception exception, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "External address service unavailable")]
    private partial void LogDependencyUnavailable(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing the request")]
    private partial void LogUnhandled(Exception exception);
}
