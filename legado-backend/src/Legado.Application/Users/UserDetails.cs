namespace Legado.Application.Users;

/// <summary>Dados completos do usuário para a tela de perfil.</summary>
/// <param name="Id">Id técnico do usuário.</param>
/// <param name="UserName">Login.</param>
/// <param name="FullName">Nome completo.</param>
/// <param name="Email">E-mail.</param>
/// <param name="MobilePhone">Celular em E.164.</param>
/// <param name="Address">Endereço residencial.</param>
/// <param name="Roles">Perfis do usuário.</param>
/// <param name="CreatedAt">Data do cadastro (UTC).</param>
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
/// <param name="Id">Id técnico do usuário.</param>
/// <param name="UserName">Login.</param>
/// <param name="FullName">Nome completo.</param>
/// <param name="Email">E-mail.</param>
/// <param name="CreatedAt">Data do cadastro (UTC).</param>
public sealed record UserSummary(Guid Id, string UserName, string FullName, string Email, DateTimeOffset CreatedAt);

/// <summary>Página de resultados.</summary>
/// <typeparam name="T">Tipo do item.</typeparam>
/// <param name="Items">Itens da página.</param>
/// <param name="Page">Número da página, a partir de 1.</param>
/// <param name="PageSize">Tamanho pedido.</param>
/// <param name="TotalCount">Total de itens em todas as páginas.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
