namespace Legado.Domain.Addresses;

/// <summary>Município identificado pelo código IBGE (7 dígitos), evitando ambiguidade entre nomes iguais.</summary>
/// <param name="IbgeCode">Código do município no IBGE.</param>
/// <param name="Name">Nome oficial do município.</param>
public sealed record City(int IbgeCode, string Name);
