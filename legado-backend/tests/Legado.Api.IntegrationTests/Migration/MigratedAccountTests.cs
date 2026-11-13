using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users.Migration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Migration;

/// <summary>Conta migrada para o Keycloak: login no Legado leva ao Cadastro (US-04 da spec 001 do cadastro).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class MigratedAccountTests(LegadoApiFactory factory)
{
    private const string CadastroUrl = "http://localhost:4300/";

    [Fact]
    public async Task Login_WithRightPasswordOfMigratedAccount_ReturnsAccountMigrated()
    {
        using var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        await MarkMigratedAsync(userName);

        using var response = await LoginAsync(client, userName, TestUsers.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await CredentialCodesAsync(response)).ShouldBe(["auth.account_migrated"]);
        (await NewSystemUrlAsync(response)).ShouldBeNull();
        factory.Logs.Events.ShouldContain(e => e.Level == LogEventLevel.Warning
            && e.MessageTemplate.Text.StartsWith("Sign-in rejected: user {UserId} was migrated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_OfMigratedAccount_ReturnsTheCadastroUrlToRedirectTo()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Migration:NewSystemUrl", CadastroUrl));
        using var client = host.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        await MarkMigratedAsync(userName);

        using var response = await LoginAsync(client, userName, TestUsers.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await NewSystemUrlAsync(response)).ShouldBe(CadastroUrl);
    }

    // O endereço só vai para quem acertou a senha da conta migrada: a resposta genérica não muda.
    [Fact]
    public async Task Login_WithWrongPassword_NeverReturnsTheCadastroUrl()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Migration:NewSystemUrl", CadastroUrl));
        using var client = host.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        await MarkMigratedAsync(userName);

        using var response = await LoginAsync(client, userName, "Errada@123");

        (await NewSystemUrlAsync(response)).ShouldBeNull();
    }

    // O endereço vira destino de redirecionamento: só http(s) absoluto passa na subida.
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://cadastro.local/")]
    public void Startup_WithUnsafeCadastroUrl_Fails(string url)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Migration:NewSystemUrl", url));

        Should.Throw<OptionsValidationException>(() => host.CreateClient());
    }

    // O aviso de migração só aparece com a senha correta: a senha errada não revela que a conta existe.
    [Fact]
    public async Task Login_WithWrongPasswordOfMigratedAccount_ReturnsTheGenericError()
    {
        using var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        await MarkMigratedAsync(userName);

        using var response = await LoginAsync(client, userName, "Errada@123");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await CredentialCodesAsync(response)).ShouldBe(["auth.invalid_credentials"]);
    }

    [Fact]
    public async Task OpenSession_StopsWorkingWhenTheAccountIsMigrated()
    {
        using var client = factory.CreateClient();
        var userName = await TestUsers.RegisterAsync(client, TestContext.Current.CancellationToken);
        var session = await TestUsers.LoginAsync(client, userName, TestContext.Current.CancellationToken);
        TestUsers.Authenticate(client, session.Token, session.FingerprintCookie);
        (await client.GetAsync("/api/v1/users/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await MarkMigratedAsync(userName);

        using var afterMigration = await client.GetAsync("/api/v1/users/me", TestContext.Current.CancellationToken);
        afterMigration.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password }, TestContext.Current.CancellationToken);

    private static async Task<string[]> CredentialCodesAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return [.. body.RootElement.GetProperty("errors").GetProperty("credentials").EnumerateArray().Select(c => c.GetString()!)];
    }

    private static async Task<string?> NewSystemUrlAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.TryGetProperty("newSystemUrl", out var url) ? url.GetString() : null;
    }

    private async Task MarkMigratedAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Legado.Infrastructure.Persistence.ApplicationUser>>();
        var user = await users.FindByNameAsync(userName) ?? throw new InvalidOperationException("Test user not found.");
        var result = await scope.ServiceProvider.GetRequiredService<IUserMigrationWriter>()
            .MarkMigratedAsync(user.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
    }
}
