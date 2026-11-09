namespace Legado.Api.Auth;

/// <summary>Nomes de claims (os padrão seguem o Keycloak, para facilitar a migração futura).</summary>
internal static class JwtClaimTypes
{
    /// <summary>Claim com o login do usuário (mesmo nome usado pelo Keycloak).</summary>
    public const string PreferredUserName = "preferred_username";

    /// <summary>Claim com cada perfil do usuário.</summary>
    public const string Role = "role";

    /// <summary>Hash SHA-256 do fingerprint guardado no cookie HttpOnly (OWASP: prevenção de token sidejacking).</summary>
    public const string FingerprintHash = "fgp";

    /// <summary>Hash SHA-256 do carimbo de segurança vigente na emissão (permite revogar tokens no logout).</summary>
    public const string SecurityStampHash = "sstamp";
}
