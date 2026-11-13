using Legado.Api.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Legado.Api.IntegrationTests.Health;

/// <summary>Testes isolados do escritor do relatório de saúde (sem subir a API).</summary>
public sealed class HealthResponseWriterTests
{
    private static readonly HealthReport HealthyReport = new(
        new Dictionary<string, HealthReportEntry>
        {
            ["sqlserver"] = new(HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(5), null, null),
        },
        TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task WriteAsync_WhenRequestIsActive_WritesJsonReport()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await HealthResponseWriter.WriteAsync(context, HealthyReport);

        context.Response.ContentType.ShouldBe("application/json");
        context.Response.Body.Length.ShouldBeGreaterThan(0);
    }

    // Regressão (Sonar S8949): o relatório era escrito mesmo depois de o cliente desistir da requisição.
    [Fact]
    public async Task WriteAsync_WhenClientAbortedRequest_StopsWithoutWriting()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() }, RequestAborted = aborted.Token };

        await Should.ThrowAsync<OperationCanceledException>(() => HealthResponseWriter.WriteAsync(context, HealthyReport));

        context.Response.Body.Length.ShouldBe(0);
    }
}
