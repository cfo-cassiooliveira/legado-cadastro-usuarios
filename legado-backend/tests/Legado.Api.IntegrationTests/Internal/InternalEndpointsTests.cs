using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Api.Internal;
using Legado.Application.Users.Migration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Internal;

/// <summary>API interna de migração (ADR-0005): contrato, assinatura HMAC, permissões por chamador e isolamento.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class InternalEndpointsTests(LegadoApiFactory factory) : IAsyncLifetime
{
    private const string Verify = "/api/v1/internal/credential-verifications";
    private const string Lookup = "/api/v1/internal/user-lookups";
    private const string Existence = "/api/v1/internal/user-existence-checks";

    private InternalApiHost _internal = null!;

    public ValueTask InitializeAsync()
    {
        _internal = new InternalApiHost(factory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _internal.DisposeAsync();

    // ---------------------------------------------------------------- verificação de credenciais

    [Fact]
    public async Task Verify_WithValidLoginAndPassword_ReturnsTheAccountWithoutPasswordHash()
    {
        var userName = await RegisterAsync();

        var response = await _internal.PostJsonAsync(Verify, new { login = userName, password = TestUsers.Password });

        response.GetProperty("outcome").GetString().ShouldBe("valid");
        var user = response.GetProperty("user");
        user.GetProperty("userName").GetString().ShouldBe(userName);
        user.GetProperty("fullName").GetString().ShouldBe("Ana Paula Souza");
        user.GetProperty("mobilePhone").GetString().ShouldBe("+5516991234567");
        user.GetProperty("address").GetProperty("postalCode").GetString().ShouldBe("01001000");
        user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldContain("User");
        response.ToString().ShouldNotContain("passwordHash", Case.Insensitive);
    }

    [Fact]
    public async Task Verify_AcceptsTheEmailAsLogin()
    {
        var userName = await RegisterAsync();

        var response = await _internal.PostJsonAsync(Verify, new { login = $"{userName}@exemplo.com".ToUpperInvariant(), password = TestUsers.Password });

        response.GetProperty("outcome").GetString().ShouldBe("valid");
    }

    [Fact]
    public async Task Verify_ReportsEachRejectionWithoutUserData()
    {
        var userName = await RegisterAsync();

        var wrongPassword = await _internal.PostJsonAsync(Verify, new { login = userName, password = "Errada@123" });
        var unknown = await _internal.PostJsonAsync(Verify, new { login = "nao.existe." + Guid.NewGuid().ToString("N")[..6], password = "Errada@123" });

        wrongPassword.GetProperty("outcome").GetString().ShouldBe("invalid_password");
        unknown.GetProperty("outcome").GetString().ShouldBe("not_found");
        wrongPassword.GetProperty("user").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Verify_AfterFiveWrongPasswords_ReportsLocked()
    {
        var userName = await RegisterAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _internal.PostJsonAsync(Verify, new { login = userName, password = "Errada@123" });
        }

        var response = await _internal.PostJsonAsync(Verify, new { login = userName, password = TestUsers.Password });

        response.GetProperty("outcome").GetString().ShouldBe("locked");
    }

    [Fact]
    public async Task Verify_WhenAccountWasMigrated_ReportsMigratedOnlyWithTheRightPassword()
    {
        var userName = await RegisterAsync();
        await MarkMigratedAsync(userName);

        var right = await _internal.PostJsonAsync(Verify, new { login = userName, password = TestUsers.Password });
        var wrong = await _internal.PostJsonAsync(Verify, new { login = userName, password = "Errada@123" });

        right.GetProperty("outcome").GetString().ShouldBe("migrated");
        wrong.GetProperty("outcome").GetString().ShouldBe("invalid_password");
    }

    // Enumeração por tempo: conta inexistente compara com hash fictício, no mesmo tempo de uma senha errada.
    [Fact]
    public async Task Verify_TakesSimilarTimeForUnknownLoginAndWrongPassword()
    {
        var userName = await RegisterAsync();
        await _internal.PostJsonAsync(Verify, new { login = "aquecimento", password = "Errada@123" });

        var unknown = await MedianMillisecondsAsync(() => _internal.PostJsonAsync(Verify, new { login = "nao.existe", password = "Errada@123" }));
        var wrong = await MedianMillisecondsAsync(async () =>
        {
            var result = await _internal.PostJsonAsync(Verify, new { login = userName, password = "Errada@123" });
            await ResetFailuresAsync(userName);
            return result;
        });

        Math.Abs(unknown - wrong).ShouldBeLessThan(Math.Max(unknown, wrong) * 0.3);
    }

    [Fact]
    public async Task Verify_WithoutLoginOrPassword_Returns400()
    {
        using var response = await _internal.SendAsync(HttpMethod.Post, Verify, new { login = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- consulta, existência e marcação

    [Fact]
    public async Task Lookup_ReportsFoundNotFoundAndMigrated()
    {
        var found = await RegisterAsync();
        var migrated = await RegisterAsync();
        await MarkMigratedAsync(migrated);

        (await _internal.PostJsonAsync(Lookup, new { login = found })).GetProperty("outcome").GetString().ShouldBe("found");
        (await _internal.PostJsonAsync(Lookup, new { login = "nao.existe" })).GetProperty("outcome").GetString().ShouldBe("not_found");
        var migratedResponse = await _internal.PostJsonAsync(Lookup, new { login = migrated });
        migratedResponse.GetProperty("outcome").GetString().ShouldBe("migrated");
        migratedResponse.GetProperty("user").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Existence_ConsidersOnlyAccountsNotYetMigrated()
    {
        var active = await RegisterAsync();
        var migrated = await RegisterAsync();
        await MarkMigratedAsync(migrated);

        var taken = await _internal.PostJsonAsync(Existence, new { userName = active, email = $"{active}@exemplo.com" }, "cadastro-1");
        var free = await _internal.PostJsonAsync(Existence, new { userName = migrated, email = $"{migrated}@exemplo.com" }, "cadastro-1");

        taken.GetProperty("userNameTaken").GetBoolean().ShouldBeTrue();
        taken.GetProperty("emailTaken").GetBoolean().ShouldBeTrue();
        free.GetProperty("userNameTaken").GetBoolean().ShouldBeFalse();
        free.GetProperty("emailTaken").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_IsIdempotentAndRejectsAnotherKeycloakUser()
    {
        var legacyId = await LegacyIdAsync(await RegisterAsync());
        var keycloakUserId = Guid.NewGuid();
        var path = $"/api/v1/internal/users/{legacyId}/migration";

        using var first = await _internal.SendAsync(HttpMethod.Put, path, new { keycloakUserId });
        using var again = await _internal.SendAsync(HttpMethod.Put, path, new { keycloakUserId });
        using var other = await _internal.SendAsync(HttpMethod.Put, path, new { keycloakUserId = Guid.NewGuid() });
        using var unknown = await _internal.SendAsync(HttpMethod.Put, $"/api/v1/internal/users/{Guid.NewGuid()}/migration", new { keycloakUserId });
        using var missing = await _internal.SendAsync(HttpMethod.Put, path, new { });

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        other.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- assinatura e permissões

    [Fact]
    public async Task Signature_Missing_Returns401()
    {
        using var response = await _internal.Client.PostAsync(Verify, null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("unknown_key")]
    [InlineData("expired")]
    [InlineData("invalid")]
    public async Task Signature_Rejected_Returns401AndLogsOnlyTheReason(string reason)
    {
        using var response = reason switch
        {
            "unknown_key" => await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "x" }, keyId: "intruso-1", secret: InternalApiHost.KeycloakSecret),
            "expired" => await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "x" }, timestamp: DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds()),
            _ => await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "x" },
                tamper: request => request.Content = new StringContent("{\"login\":\"outro\"}", System.Text.Encoding.UTF8, "application/json")),
        };

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var rejected = factory.Logs.Events.Where(e => e.MessageTemplate.Text.StartsWith("Internal API signature rejected", StringComparison.Ordinal)
            && CollectingLogSink.Scalar(e.Properties["Reason"]) == reason).ToList();
        rejected.ShouldNotBeEmpty();
        rejected.ShouldAllBe(e => e.Level == LogEventLevel.Warning);
        rejected.ShouldAllBe(e => !CollectingLogSink.FullText(e).Contains("v1=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signature_ReplayedNonce_IsRejected()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = HmacSignature.NewNonce();

        using var first = await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "replay" }, timestamp: timestamp, nonce: nonce);
        using var replay = await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "replay" }, timestamp: timestamp, nonce: nonce);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Duas chamadas legítimas idênticas no mesmo segundo (retentativa) passam: cada uma tem nonce próprio.
    [Fact]
    public async Task Signature_IdenticalCallsWithFreshNonces_AreAccepted()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var first = await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "retry" }, timestamp: timestamp);
        using var retry = await _internal.SendAsync(HttpMethod.Post, Lookup, new { login = "retry" }, timestamp: timestamp);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Troca de chave sem parada: a nova e a antiga do mesmo chamador valem ao mesmo tempo.
    [Fact]
    public async Task Signature_TwoActiveKeysForTheSameCaller_AreBothAccepted()
    {
        var newSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var rotating = new InternalApiHost(factory, configure: builder =>
        {
            builder.UseSetting("InternalApi:Keys:keycloak-2:Caller", "keycloak");
            builder.UseSetting("InternalApi:Keys:keycloak-2:Secret", newSecret);
        });

        using var oldKey = await rotating.SendAsync(HttpMethod.Post, Lookup, new { login = "x" });
        using var newKey = await rotating.SendAsync(HttpMethod.Post, Lookup, new { login = "x" }, keyId: "keycloak-2", secret: newSecret);

        oldKey.StatusCode.ShouldBe(HttpStatusCode.OK);
        newKey.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Callers_OnlyReachTheirOwnEndpoints()
    {
        using var cadastroOnVerify = await _internal.SendAsync(HttpMethod.Post, Verify, new { login = "x", password = "y" }, keyId: "cadastro-1");
        using var keycloakOnExistence = await _internal.SendAsync(HttpMethod.Post, Existence, new { userName = "x", email = "x@x.com" });

        cadastroOnVerify.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        keycloakOnExistence.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // O JWT do próprio Legado não abre o grupo interno, e a assinatura HMAC não abre as rotas públicas.
    [Fact]
    public async Task Schemes_DoNotCrossBetweenInternalAndPublicRoutes()
    {
        using var publicClient = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(publicClient, TestContext.Current.CancellationToken);
        var session = await TestUsers.LoginAsync(publicClient, userName, TestContext.Current.CancellationToken);

        using var jwtOnInternal = new HttpRequestMessage(HttpMethod.Post, Lookup) { Content = JsonContent(new { login = userName }) };
        jwtOnInternal.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        jwtOnInternal.Headers.Add("Cookie", session.FingerprintCookie);
        using var jwtResponse = await _internal.Client.SendAsync(jwtOnInternal, TestContext.Current.CancellationToken);

        using var hmacOnPublic = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        hmacOnPublic.Headers.Add(HmacSignature.HeaderName,
            HmacSignature.Header("keycloak-1", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), HmacSignature.NewNonce(), [1, 2, 3]));
        using var hmacResponse = await publicClient.SendAsync(hmacOnPublic, TestContext.Current.CancellationToken);

        jwtResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        hmacResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------- isolamento por porta

    [Fact]
    public async Task InternalRoutes_DoNotExistWhenTheInternalApiIsOff()
    {
        using var response = await factory.CreateClient().PostAsync(Lookup, JsonContent(new { login = "x" }), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InternalRoutes_OnThePublicPort_Return404EvenWhenSigned()
    {
        await using var portBound = new InternalApiHost(factory, internalPort: 8443);

        using var response = await portBound.SendAsync(HttpMethod.Post, Lookup, new { login = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicRoutes_OnTheInternalPort_Return404()
    {
        using var response = await _internal.Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<string> RegisterAsync()
    {
        // E-mail derivado do login para os testes que entram por e-mail.
        using var client = factory.CreateClient();
        var userName = "mig." + Guid.NewGuid().ToString("N")[..8];
        using var response = await client.PostAsJsonAsync("/api/v1/users",
            TestUsers.NewRegistration(userName, $"{userName}@exemplo.com"), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return userName;
    }

    private async Task<Guid> LegacyIdAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<ILegacyUserReadService>();
        return (await users.FindByLoginAsync(userName, TestContext.Current.CancellationToken))!.Value.User.Id;
    }

    private async Task MarkMigratedAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUserMigrationWriter>()
            .MarkMigratedAsync(await LegacyIdAsync(userName), Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task ResetFailuresAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Legado.Infrastructure.Persistence.ApplicationUser>>();
        await manager.ResetAccessFailedCountAsync((await manager.FindByNameAsync(userName))!);
    }

    private static async Task<double> MedianMillisecondsAsync(Func<Task<JsonElement>> call)
    {
        var samples = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await call();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), System.Text.Encoding.UTF8, "application/json");
}
