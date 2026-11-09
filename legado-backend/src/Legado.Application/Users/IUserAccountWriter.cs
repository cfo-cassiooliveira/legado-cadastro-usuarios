using Legado.Domain.Common;

namespace Legado.Application.Users;

/// <summary>Porta de criação de contas (hash da senha e unicidade ficam no provedor de identidade).</summary>
public interface IUserAccountWriter
{
    /// <summary>Cria a conta com o perfil <see cref="Roles.User"/>; retorna o id ou erros de senha/duplicidade.</summary>
    Task<Result<Guid>> CreateAsync(NewUserAccount account, string password, CancellationToken cancellationToken);
}
