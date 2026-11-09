using Legado.Domain.Addresses;
using Legado.Domain.Users;

namespace Legado.Application.Users;

/// <summary>Dados já validados para criação de uma conta.</summary>
public sealed record NewUserAccount(
    UserName UserName,
    FullName FullName,
    EmailAddress Email,
    MobilePhone MobilePhone,
    Address Address);
