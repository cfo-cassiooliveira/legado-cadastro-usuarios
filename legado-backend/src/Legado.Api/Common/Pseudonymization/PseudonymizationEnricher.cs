using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace Legado.Api.Common.Pseudonymization;

/// <summary>
/// Pseudonimiza na origem (ADR-0003 do graylog) os campos cujo nome está em <see cref="PersonalDataFields"/>, em
/// qualquer nível do evento: troca o valor pela máscara do ADR-0002 e soma o token (HMAC) como
/// propriedade de topo. Valor fora do formato, ou não escalar (objeto/lista), vira <c>***</c> sem token.
/// </summary>
/// <remarks>
/// <para><b>Por que na origem</b>, e não só no Graylog: os campos conhecidos já saem da API mascarados em qualquer
/// destino (console ou GELF). Custo: as listas de nomes são repetidas no projeto graylog e precisam andar juntas.</para>
/// <para><b>Topo do evento</b> (<c>{Cpf}</c> no template, <c>LogContext</c>, escopo com dicionário): o token vai em
/// <c>Cpf_token</c>, o campo pesquisado pelo Cadastro.</para>
/// <para><b>Aninhado</b> (<c>{@Customer}</c>, dicionário, lista): mascarado no próprio nível; o token vai no topo do
/// evento com o caminho unido por <c>_</c> (<c>Customer_Email_token</c>, <c>People_0_login_token</c>), nunca dentro do
/// objeto, para não aparecer no texto renderizado. O sink GELF envia o campo mascarado como <c>Customer.Email</c> e o
/// Graylog o grava como <c>Customer_Email</c>, ao lado de <c>Customer_Email_token</c>. Fora da consulta padrão do
/// Cadastro (só nomes de topo): dado pessoal continua indo só como campo de topo.</para>
/// <para><b>Textos</b>: <c>message</c>/<c>full_message</c> são renderizados depois, a partir das propriedades já
/// trocadas. O texto do escopo (<c>Scope</c>, já renderizado pelo provider do Microsoft.Extensions.Logging com o valor
/// em claro) recebe a troca do valor original pela máscara (ou por <c>***</c> quando CPF/CNPJ/telefone está fora do
/// formato com 8+ dígitos; login/e-mail curto ou inválido, de qualquer tamanho, não é trocado, para não corromper palavras). A exceção não é alterada.</para>
/// </remarks>
internal sealed class PseudonymizationEnricher(PseudonymTokenGenerator tokens) : ILogEventEnricher
{
    /// <summary>Propriedade em que o Serilog.Extensions.Logging guarda o texto renderizado dos escopos.</summary>
    public const string ScopeProperty = "Scope";

    private const int MinDigitsToRedactInText = 8;

    /// <summary>Pseudonimiza os campos de dado pessoal do evento (topo e aninhados) e o texto do escopo.</summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var pass = new Pass();

        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            if (PersonalDataFields.ByName.TryGetValue(name, out var kind))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, PseudonymizeField(name, kind, value, pass)));
            }
            else if (value is not ScalarValue && name != ScopeProperty)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, Walk(name, value, pass)));
            }
        }

        if (logEvent.Properties.TryGetValue(ScopeProperty, out var scope))
        {
            var walked = Walk(ScopeProperty, scope, pass);
            logEvent.AddOrUpdateProperty(new LogEventProperty(ScopeProperty, ReplaceInText(walked, pass.Replacements)));
        }

        // Tokens sempre no topo: fora de qualquer objeto renderizado no texto (message só leva a máscara).
        foreach (var token in pass.Tokens)
        {
            logEvent.AddOrUpdateProperty(token);
        }
    }

    /// <summary>
    /// Devolve o valor mascarado (ou <c>***</c>) e registra o token de topo <c>&lt;path&gt;_token</c>, onde
    /// <c>path</c> é o nome do campo no topo ou o caminho aninhado unido por <c>_</c> (ex.: <c>Customer_Email</c>).
    /// </summary>
    private LogEventPropertyValue PseudonymizeField(string path, PersonalDataKind kind, LogEventPropertyValue value, Pass pass)
    {
        if (value is ScalarValue { Value: null })
        {
            return value;
        }

        var raw = value is ScalarValue scalar ? Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) : null;
        if (raw is not null && PersonalDataMasker.TryCanonicalize(kind, raw, out var canonical))
        {
            var mask = PersonalDataMasker.Mask(kind, canonical);
            pass.Replacements.Add(KeyValuePair.Create(raw, mask));
            pass.Tokens.Add(new LogEventProperty(path + PersonalDataFields.TokenSuffix, new ScalarValue(tokens.Generate(kind, canonical))));
            return new ScalarValue(mask);
        }

        if (raw is not null && kind != PersonalDataKind.Login && raw.Count(char.IsAsciiDigit) >= MinDigitsToRedactInText)
        {
            pass.Replacements.Add(KeyValuePair.Create(raw, PersonalDataMasker.Redacted));
        }

        return new ScalarValue(PersonalDataMasker.Redacted);
    }

    private LogEventPropertyValue Walk(string path, LogEventPropertyValue value, Pass pass) =>
        value switch
        {
            StructureValue structure => new StructureValue(
                structure.Properties.Select(p => new LogEventProperty(p.Name, WalkMember(Child(path, p.Name), p.Name, p.Value, pass))).ToList(),
                structure.TypeTag),
            DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.Select(e => KeyValuePair.Create(e.Key,
                e.Key.Value is string key ? WalkMember(Child(path, key), key, e.Value, pass) : Walk(path, e.Value, pass))).ToList()),
            SequenceValue sequence => new SequenceValue(
                sequence.Elements.Select((e, index) => Walk(Child(path, index.ToString(CultureInfo.InvariantCulture)), e, pass)).ToList()),
            _ => value,
        };

    private LogEventPropertyValue WalkMember(string path, string name, LogEventPropertyValue value, Pass pass) =>
        PersonalDataFields.ByName.TryGetValue(name, out var kind) ? PseudonymizeField(path, kind, value, pass) : Walk(path, value, pass);

    // "_" e não ".": o Graylog troca o ponto dos nomes GELF por "_" (Customer.Email é gravado como Customer_Email).
    private static string Child(string path, string name) => $"{path}_{name}";

    private static LogEventPropertyValue ReplaceInText(LogEventPropertyValue value, List<KeyValuePair<string, string>> replacements) =>
        value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(Replace(text, replacements)),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(e => ReplaceInText(e, replacements)).ToList()),
            _ => value,
        };

    private static string Replace(string text, List<KeyValuePair<string, string>> replacements) =>
        // O valor mais longo primeiro: evita trocar só um pedaço de outro valor que o contém.
        replacements.OrderByDescending(r => r.Key.Length)
            .Aggregate(text, (current, r) => current.Replace(r.Key, r.Value, StringComparison.Ordinal));

    /// <summary>Estado de uma passada: valores a trocar nos textos e tokens a somar no topo do evento.</summary>
    private sealed class Pass
    {
        public List<KeyValuePair<string, string>> Replacements { get; } = [];

        public List<LogEventProperty> Tokens { get; } = [];
    }
}
