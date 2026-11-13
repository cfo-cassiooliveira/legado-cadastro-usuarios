namespace Legado.Domain.Common;

/// <summary>
/// Erro esperado de negócio. <see cref="Code"/> é estável e traduzido pelo front (i18n);
/// <see cref="Description"/> é técnica, em inglês.
/// </summary>
/// <remarks>Propriedades só de leitura (sem <c>init</c>): no Domain o estado só nasce pelo construtor.</remarks>
public sealed record DomainError
{
    /// <summary>Cria o erro com código, campo, descrição técnica e categoria.</summary>
    /// <param name="code">Código estável do erro (ex.: <c>postal_code.invalid</c>).</param>
    /// <param name="field">Campo do contrato ao qual o erro se refere.</param>
    /// <param name="description">Descrição técnica em inglês.</param>
    /// <param name="type">Categoria do erro.</param>
    public DomainError(string code, string field, string description, ErrorType type = ErrorType.Validation)
    {
        Code = code;
        Field = field;
        Description = description;
        Type = type;
    }

    /// <summary>Código estável do erro (ex.: <c>postal_code.invalid</c>).</summary>
    public string Code { get; }

    /// <summary>Campo do contrato ao qual o erro se refere.</summary>
    public string Field { get; }

    /// <summary>Descrição técnica em inglês.</summary>
    public string Description { get; }

    /// <summary>Categoria do erro.</summary>
    public ErrorType Type { get; }
}
