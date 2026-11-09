using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Legado.Api.IntegrationTests.Security;

/// <summary>
/// Cenários de ataque ao token: roubo (sem cookie), replay após logout, conta bloqueada,
/// adulteração e "alg: none". Todos devem resultar em 401.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class TokenSecurityTests(LegadoApiFactory factory)
{
    private const string MeRoute = "/api/v1/users/me";

    [Fact]
    public async Task Login_SetsHardenedFingerprintCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);

        var attributes = session.SetCookieHeader.ToLowerInvariant();
        attributes.ShouldContain("httponly");
        attributes.ShouldContain("secure");
        attributes.ShouldContain("samesite=strict");
        attributes.ShouldContain("path=/");
        attributes.ShouldNotContain("domain=");
    }

    [Fact]
    public async Task Login_ResponseBodyDoesNotExposeFingerprint()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = TestUsers.Password }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var fingerprint = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1];

        body.ShouldNotContain(fingerprint);
        body.ShouldNotContain("fingerprint", Case.Insensitive);
    }

    [Fact]
    public async Task Token_ContainsOnlyHashesAndNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);

        using var payload = DecodePayload(session.Token);
        var claims = payload.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        // Minimização de dados (LGPD): sem nome, e-mail, celular ou endereço no token.
        claims.ShouldNotContain("name");
        claims.ShouldNotContain("email");
        payload.RootElement.GetProperty("fgp").GetString()!.Length.ShouldBe(64);
        payload.RootElement.GetProperty("sstamp").GetString()!.Length.ShouldBe(64);
        session.Token.ShouldNotContain(session.FingerprintCookie.Split('=', 2)[1]);
    }

    [Fact]
    public async Task StolenToken_WithoutFingerprintCookie_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);

        var attacker = factory.CreateClient();
        attacker.Authenticate(session.Token, fingerprintCookie: null);

        (await attacker.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StolenToken_WithAnotherSessionsCookie_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var victim = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        var attackerSession = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);

        var attacker = factory.CreateClient();
        attacker.Authenticate(victim.Token, attackerSession.FingerprintCookie);

        (await attacker.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTokenOnServer_ReplayIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        client.Authenticate(session);
        (await client.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var logout = await client.PostAsync("/api/v1/auth/logout", content: null, ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").ShouldContain(h => h.StartsWith(TestUsers.FingerprintCookieName + "=;", StringComparison.Ordinal));
        // Mesmo token + mesmo cookie, ainda dentro da validade: recusado porque a sessão foi revogada.
        (await client.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesAllSessionsOfTheUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        var desktop = await TestUsers.LoginAsync(client, userName, ct);
        var phone = await TestUsers.LoginAsync(client, userName, ct);
        var desktopClient = factory.CreateClient();
        desktopClient.Authenticate(desktop);

        await desktopClient.PostAsync("/api/v1/auth/logout", content: null, ct);

        var phoneClient = factory.CreateClient();
        phoneClient.Authenticate(phone);
        (await phoneClient.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LockedAccount_LosesAccessImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        client.Authenticate(await TestUsers.LoginAsync(client, userName, ct));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(userName) ?? throw new InvalidOperationException("User not found.");
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
        }

        (await client.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnsignedToken_AlgNone_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        var payload = session.Token.Split('.')[1];
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");

        client.Authenticate($"{header}.{payload}.", session.FingerprintCookie);

        (await client.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ElevatedRoleInTamperedPayload_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        var parts = session.Token.Split('.');
        var forgedPayload = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1]))).Replace("\"User\"", "\"Admin\"", StringComparison.Ordinal);

        client.Authenticate($"{parts[0]}.{Base64Url(forgedPayload)}.{parts[2]}", session.FingerprintCookie);

        (await client.GetAsync("/api/v1/users", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiResponses_AreNotCacheable_AndHideServerHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        client.Authenticate(await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct));

        var response = await client.GetAsync(MeRoute, ct);

        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.Contains("Server").ShouldBeFalse();
    }

    private static JsonDocument DecodePayload(string token) =>
        JsonDocument.Parse(Convert.FromBase64String(Pad(token.Split('.')[1])));

    private static string Pad(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        return base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
    }

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
