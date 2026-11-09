using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;

namespace Legado.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AuthEndpointsTests(LegadoApiFactory factory)
{
    private const int MaxFailedAccessAttempts = 5;

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedProblem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = "Errada@123" }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync(ct)).ShouldContain("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { userName = "nao.existe", password = TestUsers.Password }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterTooManyFailures_LocksOutEvenWithCorrectPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);

        for (var attempt = 0; attempt < MaxFailedAccessAttempts; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = "Errada@123" }, ct);
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = TestUsers.Password }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsTokenWithKeycloakCompatibleClaims()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);

        var token = (await TestUsers.LoginAsync(client, userName, ct)).Token;

        using var payload = JsonDocument.Parse(DecodeSegment(token.Split('.')[1]));
        payload.RootElement.GetProperty("preferred_username").GetString().ShouldBe(userName);
        payload.RootElement.GetProperty("iss").GetString().ShouldBe("legado-api");
        payload.RootElement.GetProperty("aud").GetString().ShouldBe("legado-web");
        payload.RootElement.GetProperty("role").GetString().ShouldBe("User");
    }

    private static byte[] DecodeSegment(string segment)
    {
        // Base64url sem padding -> Base64 padrão.
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
