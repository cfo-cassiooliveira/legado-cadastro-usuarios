using Microsoft.AspNetCore.Identity;

namespace Legado.Infrastructure.Persistence;

/// <summary>
/// Usuário do legado persistido pelo ASP.NET Core Identity (tabela <c>AspNetUsers</c>).
/// O <c>PasswordHash</c> segue o formato Identity V3 (PBKDF2-HMAC-SHA512), importável no Keycloak.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nome completo do usuário.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Endereço residencial (colunas <c>Address*</c>).</summary>
    public UserAddress Address { get; set; } = new();

    /// <summary>
    /// Quando a conta passou a ser gerida pelo Keycloak (UTC); preenchida só depois que o usuário tem senha lá
    /// (ADR-0005). Conta migrada não entra mais no Legado.
    /// </summary>
    public DateTimeOffset? MigratedAt { get; set; }

    /// <summary>Id do usuário correspondente no Keycloak, gravado junto com <see cref="MigratedAt"/>.</summary>
    public Guid? KeycloakUserId { get; set; }

    /// <summary>Data de criação (UTC), preenchida pelo interceptor de auditoria.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Data da última alteração (UTC), preenchida pelo interceptor de auditoria.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
