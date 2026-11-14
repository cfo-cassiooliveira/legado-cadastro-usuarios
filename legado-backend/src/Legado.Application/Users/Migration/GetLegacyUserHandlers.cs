namespace Legado.Application.Users.Migration;

/// <summary>Consulta da conta por login ou e-mail (reset de senha do Keycloak).</summary>
public interface IGetLegacyUserByLoginHandler
{
    /// <summary>Devolve a conta se existir e não estiver migrada.</summary>
    Task<LegacyLookupResult> HandleAsync(string login, CancellationToken cancellationToken);
}

/// <summary>Consulta de login e e-mail em uso por contas não migradas (autocadastro do Cadastro).</summary>
public interface IGetLegacyUserExistenceHandler
{
    /// <summary>Indica se login e e-mail estão em uso no Legado.</summary>
    Task<LegacyUserExistence> HandleAsync(string userName, string email, CancellationToken cancellationToken);
}

/// <summary>Lê a conta pela porta de leitura; conta migrada não devolve dados (o Keycloak já é a fonte).</summary>
internal sealed class GetLegacyUserByLoginHandler(ILegacyUserReadService users) : IGetLegacyUserByLoginHandler
{
    /// <inheritdoc />
    public async Task<LegacyLookupResult> HandleAsync(string login, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        return await users.FindByLoginAsync(login.Trim(), cancellationToken) switch
        {
            null => new LegacyLookupResult(LegacyLookupOutcome.NotFound),
            { IsMigrated: true } => new LegacyLookupResult(LegacyLookupOutcome.Migrated),
            { User: var user } => new LegacyLookupResult(LegacyLookupOutcome.Found, user),
        };
    }
}

/// <summary>Consulta de existência pela porta de leitura (conta migrada conta como livre: o Keycloak já barra).</summary>
internal sealed class GetLegacyUserExistenceHandler(ILegacyUserReadService users) : IGetLegacyUserExistenceHandler
{
    /// <inheritdoc />
    public Task<LegacyUserExistence> HandleAsync(string userName, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return users.CheckExistenceAsync(userName.Trim(), email.Trim(), cancellationToken);
    }
}
