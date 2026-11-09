namespace Legado.Application.Users;

/// <summary>Dados completos do usuário para a tela de perfil.</summary>
public sealed record UserDetails(
    Guid Id,
    string UserName,
    string FullName,
    string Email,
    string MobilePhone,
    UserAddressDto Address,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

/// <summary>Endereço do usuário no contrato da API.</summary>
/// <param name="PostalCode">CEP (somente dígitos).</param>
/// <param name="Street">Logradouro.</param>
/// <param name="Number">Número do imóvel.</param>
/// <param name="Complement">Complemento opcional.</param>
/// <param name="District">Bairro.</param>
/// <param name="City">Nome do município.</param>
/// <param name="CityIbgeCode">Código IBGE do município.</param>
/// <param name="State">Sigla da UF.</param>
public sealed record UserAddressDto(
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    int CityIbgeCode,
    string State);

/// <summary>Linha da listagem administrativa de usuários.</summary>
public sealed record UserSummary(Guid Id, string UserName, string FullName, string Email, DateTimeOffset CreatedAt);

/// <summary>Página de resultados.</summary>
/// <typeparam name="T">Tipo do item.</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
