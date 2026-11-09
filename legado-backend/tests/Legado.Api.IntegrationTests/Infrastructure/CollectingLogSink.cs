using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>Sink do Serilog que guarda os eventos em memória para os testes inspecionarem o conteúdo real dos logs.</summary>
public sealed class CollectingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => [.. _events];

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>Eventos que têm a propriedade com o valor indicado (ex.: correlation_id do teste).</summary>
    public IReadOnlyList<LogEvent> WithProperty(string name, string value) =>
        [.. Events.Where(e => e.Properties.TryGetValue(name, out var p) && Scalar(p) == value)];

    /// <summary>Valor "cru" de uma propriedade escalar (sem as aspas que o Serilog adiciona ao renderizar strings).</summary>
    public static string? Scalar(LogEventPropertyValue value) =>
        value is ScalarValue scalar ? scalar.Value?.ToString() : value.ToString();

    /// <summary>Texto completo de um evento: mensagem renderizada, exceção e todas as propriedades.</summary>
    public static string FullText(LogEvent logEvent) =>
        string.Join(" | ", [logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture), logEvent.Exception?.ToString() ?? string.Empty,
            .. logEvent.Properties.Select(p => $"{p.Key}={p.Value}")]);
}
