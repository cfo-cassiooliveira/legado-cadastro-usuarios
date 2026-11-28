using Legado.Domain.Addresses;
using Legado.Domain.Users;

namespace Legado.Application.Users;

/// <summary>Dados já validados para criação de uma conta (a senha vai à parte, direto para o hash).</summary>
/// <param name="UserName">Login.</param>
/// <param name="FullName">Nome completo.</param>
/// <param name="Email">E-mail.</param>
/// <param name="MobilePhone">Celular.</param>
/// <param name="Address">Endereço conferido nas fontes oficiais.</param>
public sealed record NewUserAccount(
    UserName UserName,
    FullName FullName,
    EmailAddress Email,
    MobilePhone MobilePhone,
    Address Address);
