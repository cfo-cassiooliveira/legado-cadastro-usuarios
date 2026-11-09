namespace Legado.Application.Users;

/// <summary>Perfis do sistema legado (serão mapeados para realm roles no Keycloak).</summary>
public static class Roles
{
    /// <summary>Administrador: pode listar todos os usuários.</summary>
    public const string Admin = "Admin";
    /// <summary>Usuário comum: acessa apenas os próprios dados.</summary>
    public const string User = "User";
}
