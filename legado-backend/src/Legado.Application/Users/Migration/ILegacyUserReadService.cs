namespace Legado.Application.Users.Migration;

/// <summary>Porta de leitura das contas do Legado para a migração (consultas sem alterar estado).</summary>
public interface ILegacyUserReadService
{
    /// <summary>Resolve login ou e-mail para o login da conta; <c>null</c> se não existir.</summary>
    Task<string?> FindUserNameAsync(string login, CancellationToken cancellationToken);

    /// <summary>Busca a conta por login ou e-mail, informando se já foi migrada; <c>null</c> se não existir.</summary>
    Task<(LegacyUser User, bool IsMigrated)?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    /// <summary>Indica se login e e-mail estão em uso por contas ainda não migradas.</summary>
    Task<LegacyUserExistence> CheckExistenceAsync(string userName, string email, CancellationToken cancellationToken);
}
