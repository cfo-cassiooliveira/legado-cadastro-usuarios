using System.Net;
using System.Net.Http.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Legado.Api.IntegrationTests.Security;

/// <summary>Força bruta no login é limitada por IP (429 após o limite da janela).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(LegadoApiFactory factory)
{
    private const int Limit = 3;

    [Fact]
    public async Task Login_BeyondTheLimit_ReturnsTooManyRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:CredentialsPermitsPerMinute", Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var client = limited.CreateClient();
        var credentials = new { userName = "nao.existe", password = "Errada@123" };

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt <= Limit; attempt++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/login", credentials, ct)).StatusCode);
        }

        statuses.Take(Limit).ShouldAllBe(status => status == HttpStatusCode.Unauthorized);
        statuses[Limit].ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
