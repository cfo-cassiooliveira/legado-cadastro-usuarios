using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Legado.Api.IntegrationTests.Users;

[Collection(ApiTestGroup.Name)]
public sealed class UserEndpointsTests(LegadoApiFactory factory)
{
    // Primeiro byte do hash no formato Identity V3 (PBKDF2-HMAC-SHA512), base da migração para o Keycloak.
    private const byte IdentityV3FormatMarker = 0x01;

    [Fact]
    public async Task Register_ThenLoginAndGetMe_ReturnsProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/v1/users", TestUsers.NewRegistration("maria.teste"), ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        client.Authenticate(await TestUsers.LoginAsync(client, "MARIA.TESTE", ct));
        var me = await client.GetFromJsonAsync<UserDetails>("/api/v1/users/me", ct);

        me.ShouldNotBeNull();
        me.UserName.ShouldBe("maria.teste");
        me.FullName.ShouldBe("Ana Paula Souza");
        me.MobilePhone.ShouldBe("+5516991234567");
        me.Address.PostalCode.ShouldBe("01001000");
        me.Address.City.ShouldBe("São Paulo");
        me.Address.CityIbgeCode.ShouldBe(FakePostalCodeLookup.SaoPauloIbgeCode);
        me.Roles.ShouldBe([Roles.User]);
    }

    [Fact]
    public async Task Register_StoresPasswordInIdentityV3Format()
    {
        var ct = TestContext.Current.CancellationToken;
        var userName = await TestUsers.RegisterAsync(factory.CreateClient(), ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserName == userName, ct);

        user.PasswordHash.ShouldNotBeNullOrEmpty();
        Convert.FromBase64String(user.PasswordHash)[0].ShouldBe(IdentityV3FormatMarker);
        user.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task Register_WithDuplicateUserName_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);

        var response = await client.PostAsJsonAsync("/api/v1/users", TestUsers.NewRegistration(userName), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadErrorCodesAsync(response, "userName", ct)).ShouldContain("user_name.taken");
    }

    [Fact]
    public async Task Register_WithInvalidData_ReturnsValidationProblemWithCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var payload = new { fullName = "Ana", userName = "an", password = "", email = "x", mobilePhone = "1", postalCode = "1", state = "SP", cityIbgeCode = 0 };

        var response = await client.PostAsJsonAsync("/api/v1/users", payload, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadErrorCodesAsync(response, "fullName", ct)).ShouldContain("full_name.surname_required");
        (await ReadErrorCodesAsync(response, "postalCode", ct)).ShouldContain("postal_code.invalid");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsPasswordPolicyErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var registration = JsonSerializer.SerializeToNode(TestUsers.NewRegistration())!.AsObject();
        registration["password"] = "fraca";

        var response = await client.PostAsJsonAsync("/api/v1/users", registration, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadErrorCodesAsync(response, "password", ct)).ShouldContain("password.too_short");
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/users/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithTamperedToken_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var session = await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct);
        client.Authenticate(session.Token[..^2] + "xx", session.FingerprintCookie);

        var response = await client.GetAsync("/api/v1/users/me", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_AsRegularUser_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        client.Authenticate(await TestUsers.LoginAsync(client, await TestUsers.RegisterAsync(client, ct), ct));

        var response = await client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsPagedUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        await PromoteToAdminAsync(userName);
        client.Authenticate(await TestUsers.LoginAsync(client, userName, ct));

        var page = await client.GetFromJsonAsync<PagedResult<UserSummary>>("/api/v1/users?page=1&pageSize=5", ct);

        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(5);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        page.Items.Count.ShouldBeInRange(1, 5);
    }

    [Fact]
    public async Task ListUsers_WithInvalidPageSize_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, ct);
        await PromoteToAdminAsync(userName);
        client.Authenticate(await TestUsers.LoginAsync(client, userName, ct));

        var response = await client.GetAsync("/api/v1/users?pageSize=1000", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task PromoteToAdminAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName) ?? throw new InvalidOperationException("User not found.");
        (await userManager.AddToRoleAsync(user, Roles.Admin)).Succeeded.ShouldBeTrue();
    }

    private static async Task<string[]> ReadErrorCodesAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("errors").TryGetProperty(field, out var codes)
            ? [.. codes.EnumerateArray().Select(c => c.GetString() ?? string.Empty)]
            : [];
    }
}
