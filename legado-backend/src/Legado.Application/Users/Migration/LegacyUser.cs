namespace Legado.Application.Users.Migration;

/// <summary>Dados da conta do Legado entregues ao Keycloak na migração (ADR-0005). Nunca inclui o hash da senha.</summary>
/// <param name="Id">Id técnico da conta no Legado.</param>
/// <param name="UserName">Login.</param>
/// <param name="Email">E-mail.</param>
/// <param name="EmailConfirmed">Se o e-mail foi confirmado no Legado.</param>
/// <param name="FullName">Nome completo.</param>
/// <param name="MobilePhone">Celular em E.164.</param>
/// <param name="Address">Endereço residencial.</param>
/// <param name="Roles">Perfis do Legado (<c>Admin</c>, <c>User</c>).</param>
public sealed record LegacyUser(
    Guid Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    string FullName,
    string MobilePhone,
    UserAddressDto Address,
    IReadOnlyList<string> Roles);

/// <summary>Resultado da verificação de credenciais pedida pelo Keycloak.</summary>
public enum LegacyCredentialOutcome
{
    /// <summary>Login e senha conferem e a conta ainda não foi migrada.</summary>
    Valid,

    /// <summary>Não existe conta com esse login ou e-mail.</summary>
    NotFound,

    /// <summary>A conta existe e a senha não confere.</summary>
    InvalidPassword,

    /// <summary>A conta está bloqueada por excesso de tentativas.</summary>
    Locked,

    /// <summary>A senha confere, mas a conta já foi migrada para o Keycloak.</summary>
    Migrated,
}

/// <summary>Resposta da verificação: o resultado e, só quando válido, os dados da conta.</summary>
/// <param name="Outcome">Resultado da verificação.</param>
/// <param name="User">Dados da conta (só em <see cref="LegacyCredentialOutcome.Valid"/>).</param>
public sealed record LegacyCredentialResult(LegacyCredentialOutcome Outcome, LegacyUser? User = null);

/// <summary>Resultado da consulta de conta por login (usada no reset de senha).</summary>
public enum LegacyLookupOutcome
{
    /// <summary>Conta encontrada e ainda não migrada.</summary>
    Found,

    /// <summary>Não existe conta com esse login ou e-mail.</summary>
    NotFound,

    /// <summary>A conta já foi migrada para o Keycloak.</summary>
    Migrated,
}

/// <summary>Resposta da consulta: o resultado e, só quando encontrada, os dados da conta.</summary>
/// <param name="Outcome">Resultado da consulta.</param>
/// <param name="User">Dados da conta (só em <see cref="LegacyLookupOutcome.Found"/>).</param>
public sealed record LegacyLookupResult(LegacyLookupOutcome Outcome, LegacyUser? User = null);

/// <summary>Se login e e-mail estão em uso por contas do Legado ainda não migradas (autocadastro do Cadastro).</summary>
/// <param name="UserNameTaken">Login em uso.</param>
/// <param name="EmailTaken">E-mail em uso.</param>
public sealed record LegacyUserExistence(bool UserNameTaken, bool EmailTaken);
