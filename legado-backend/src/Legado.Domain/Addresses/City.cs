namespace Legado.Domain.Addresses;

/// <summary>Município identificado pelo código IBGE (7 dígitos), evitando ambiguidade entre nomes iguais.</summary>
/// <remarks>Propriedades só de leitura (sem <c>init</c>): no Domain o estado só nasce pelo construtor.</remarks>
public sealed record City
{
    /// <summary>Cria o município a partir do código e do nome oficiais do IBGE.</summary>
    /// <param name="ibgeCode">Código do município no IBGE.</param>
    /// <param name="name">Nome oficial do município.</param>
    public City(int ibgeCode, string name)
    {
        IbgeCode = ibgeCode;
        Name = name;
    }

    /// <summary>Código do município no IBGE.</summary>
    public int IbgeCode { get; }

    /// <summary>Nome oficial do município.</summary>
    public string Name { get; }
}
