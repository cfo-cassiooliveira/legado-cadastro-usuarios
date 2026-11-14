namespace Legado.Api.Internal;

/// <summary>
/// API interna de migração (seção <c>InternalApi</c>, ADR-0005). Desligada por padrão: o Legado sobe sozinho, sem porta
/// interna, até o override <c>docker-compose.identity.yml</c> ligar a integração.
/// </summary>
public sealed class InternalApiOptions
{
    /// <summary>Seção de configuração (<c>InternalApi</c>).</summary>
    public const string SectionName = "InternalApi";

    /// <summary>Liga a porta HTTPS interna e os endpoints <c>/api/v1/internal</c>.</summary>
    public bool Enabled { get; init; }

    /// <summary>Porta HTTPS exclusiva dos endpoints internos (nunca publicada pelo nginx).</summary>
    public int Port { get; init; } = 8443;

    /// <summary>Porta HTTP pública (atrás do nginx), mantida quando a porta interna é ligada.</summary>
    public int PublicHttpPort { get; init; } = 8080;

    /// <summary>Certificado TLS (PEM) emitido pela CA local do Legado (<c>scripts/internal-credentials.sh</c>).</summary>
    public string? CertificatePath { get; init; }

    /// <summary>Chave privada (PEM) do certificado TLS.</summary>
    public string? CertificateKeyPath { get; init; }

    /// <summary>Janela de validade da assinatura (relógio do chamador × relógio do Legado).</summary>
    public TimeSpan SignatureWindow { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Chaves HMAC por id (<c>keycloak-1</c>, <c>cadastro-1</c>...): até duas ativas por chamador para trocar sem parada.</summary>
    public Dictionary<string, InternalApiKey> Keys { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Chave HMAC de um sistema chamador.</summary>
public sealed class InternalApiKey
{
    /// <summary>Sistema dono da chave: <c>keycloak</c> ou <c>cadastro</c> (define o que ele pode chamar).</summary>
    public string Caller { get; init; } = string.Empty;

    /// <summary>Segredo de 256 bits em Base64 (só no <c>.env</c>/<c>secrets/</c>, nunca versionado).</summary>
    public string Secret { get; init; } = string.Empty;
}
