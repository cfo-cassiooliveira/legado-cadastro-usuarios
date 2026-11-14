using Legado.Domain.Common;

namespace Legado.Api.Common;

/// <summary>Converte falhas do Result pattern em Problem Details (RFC 9457).</summary>
internal static class ResultHttpExtensions
{
    /// <summary>
    /// Gera a resposta HTTP de erro a partir do primeiro tipo de erro (400, 401, 404 ou 409), com os códigos por campo e,
    /// quando informados, campos extras do contrato (ex.: <c>newSystemUrl</c> da conta migrada).
    /// </summary>
    public static IResult ToProblem(this Result result, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result into a problem.");
        }

        // Os códigos (não mensagens) vão no contrato: o front traduz via i18n (Art. I.3).
        var errors = result.Errors
            .GroupBy(e => e.Field, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Code).ToArray(), StringComparer.Ordinal);

        var primary = result.Errors[0].Type;
        if (primary == ErrorType.Validation)
        {
            return TypedResults.ValidationProblem(errors, title: "One or more validation errors occurred.");
        }

        var (status, title) = primary switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Resource conflict."),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Authentication failed."),
            _ => (StatusCodes.Status400BadRequest, "Request failed."),
        };

        return TypedResults.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>(extensions ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
            {
                ["errors"] = errors,
            });
    }
}
