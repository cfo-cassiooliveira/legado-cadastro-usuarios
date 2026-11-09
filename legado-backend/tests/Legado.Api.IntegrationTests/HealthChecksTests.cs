using System.Net;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Legado.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class HealthChecksTests(LegadoApiFactory factory)
{
    [Fact]
    public async Task Live_ReturnsHealthyWithoutCheckingDependencies()
    {
        var response = await factory.CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_ChecksOnlyTheDatabase()
    {
        using var report = await GetReportAsync("/health/ready");

        report.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        var checks = report.RootElement.GetProperty("checks");
        checks.GetProperty("sqlserver").GetProperty("status").GetString().ShouldBe("Healthy");
        checks.TryGetProperty("viacep", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Detailed_ReportsDatabaseExternalServicesAndProcessMetrics()
    {
        using var report = await GetReportAsync("/health");

        var checks = report.RootElement.GetProperty("checks");
        checks.GetProperty("sqlserver").GetProperty("status").GetString().ShouldBe("Healthy");
        checks.GetProperty("viacep").GetProperty("status").GetString().ShouldBe("Healthy");
        checks.GetProperty("ibge").GetProperty("status").GetString().ShouldBe("Healthy");
        var process = checks.GetProperty("process");
        process.GetProperty("data").GetProperty("allocated_mb").GetInt64().ShouldBeGreaterThan(0);
        process.GetProperty("data").GetProperty("uptime_seconds").GetInt64().ShouldBeGreaterThanOrEqualTo(0);
        report.RootElement.GetProperty("totalDurationMs").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Detailed_DoesNotLeakConnectionDetails()
    {
        var body = await factory.CreateClient().GetStringAsync("/health", TestContext.Current.CancellationToken);

        body.ShouldNotContain("Password", Case.Insensitive);
        body.ShouldNotContain("Server=", Case.Insensitive);
        body.ShouldNotContain("exception", Case.Insensitive);
    }

    private async Task<JsonDocument> GetReportAsync(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // Regressão: corpo com JSON/UTF-8 inválido retornava 500 em vez de 400.
    [Fact]
    public async Task MalformedJsonBody_ReturnsBadRequestProblem()
    {
        // {"street":"Pra<0xE7>a"} em Latin-1: propriedade conhecida obriga o parser a decodificar o valor.
        byte[] body = [.. "{\"street\":\"Pra"u8, 0xE7, .. "a\"}"u8];
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        // Em Development as Minimal APIs lançam BadHttpRequestException (ThrowOnBadRequest), que chegava ao handler global.
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var response = await developmentFactory.CreateClient().PostAsync("/api/v1/users", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Response_EchoesSafeCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem().ShouldBe("test-correlation-123");
    }

    [Fact]
    public async Task Response_ReplacesUnsafeCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad id\r\ninjected");

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem().ShouldNotContain("injected");
    }
}
