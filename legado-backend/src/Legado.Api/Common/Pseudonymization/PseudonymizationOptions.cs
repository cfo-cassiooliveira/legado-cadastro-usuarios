namespace Legado.Api.Common.Pseudonymization;

/// <summary>
/// Chaves HMAC da pseudonimização dos logs (ADR-0003 do graylog), em <c>Logging:Pseudonymization</c>. A mesma chave
/// por ambiente no legado e no cadastro; valores só em user-secrets/variável de ambiente, nunca no repositório.
/// </summary>
internal sealed class PseudonymizationOptions
{
    /// <summary>Seção de configuração (<c>Logging:Pseudonymization</c>).</summary>
    public const string SectionName = "Logging:Pseudonymization";

    /// <summary>Tamanho mínimo da chave: 256 bits.</summary>
    public const int MinKeyBytes = 32;

    /// <summary>
    /// Id da chave usada para emitir tokens (<c>k1</c>, <c>k2</c>...). Na rotação anual passa a <c>k(N+1)</c>. O legado só
    /// emite (não pesquisa), então a chave anterior pode sair de <see cref="Keys"/> já na troca; quem pesquisa (Cadastro) a
    /// mantém até o fim da retenção (30 dias).
    /// </summary>
    public string CurrentKeyId { get; set; } = string.Empty;

    /// <summary>Id da chave → chave em Base64 (ex.: <c>Logging__Pseudonymization__Keys__k1</c>). Valor vazio = chave ausente.</summary>
    public Dictionary<string, string> Keys { get; init; } = new(StringComparer.Ordinal);
}
