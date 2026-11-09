namespace Legado.Domain.Common;

/// <summary>
/// Erro esperado de negócio. <paramref name="Code"/> é estável e traduzido pelo front (i18n);
/// <paramref name="Description"/> é técnica, em inglês.
/// </summary>
/// <param name="Code">Código estável do erro (ex.: <c>postal_code.invalid</c>).</param>
/// <param name="Field">Campo do contrato ao qual o erro se refere.</param>
/// <param name="Description">Descrição técnica em inglês.</param>
/// <param name="Type">Categoria do erro.</param>
public sealed record DomainError(string Code, string Field, string Description, ErrorType Type = ErrorType.Validation);
