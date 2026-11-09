using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>Sessão de teste: o token e o cookie de fingerprint emitidos juntos no login.</summary>
/// <param name="Token">JWT devolvido no corpo do login.</param>
/// <param name="FingerprintCookie">Par "nome=valor" do cookie HttpOnly emitido no login.</param>
/// <param name="SetCookieHeader">Header Set-Cookie completo (para validar os atributos de segurança).</param>
public sealed record TestSession(string Token, string FingerprintCookie, string SetCookieHeader);

/// <summary>Builders de dados de teste (dados fictícios) e atalhos de autenticação.</summary>
public static class TestUsers
{
    public const string Password = "Senha@123";
    public const string FingerprintCookieName = "__Host-legado-fgp";

    /// <summary>Gera login e e-mail únicos para isolar os testes que compartilham o banco.</summary>
    public static object NewRegistration(string? userName = null, string? email = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new
        {
            fullName = "Ana Paula Souza",
            userName = userName ?? $"user.{suffix}",
            password = Password,
            email = email ?? $"user.{suffix}@exemplo.com",
            mobilePhone = "(16) 99123-4567",
            postalCode = "01001-000",
            street = "Praça da Sé",
            number = "100",
            complement = "Sala 1",
            district = "Sé",
            cityIbgeCode = FakePostalCodeLookup.SaoPauloIbgeCode,
            state = "SP",
        };
    }

    public static async Task<string> RegisterAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"user.{suffix}";
        var response = await client.PostAsJsonAsync("/api/v1/users", NewRegistration(userName), cancellationToken);
        response.EnsureSuccessStatusCode();
        return userName;
    }

    /// <summary>Faz login e captura token + cookie (o cookie é Secure, então o HttpClient de teste não o reenvia sozinho).</summary>
    public static async Task<TestSession> LoginAsync(HttpClient client, string userName, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password = Password }, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = body.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Login response without token.");
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith(FingerprintCookieName, StringComparison.Ordinal));
        return new TestSession(token, setCookie.Split(';')[0], setCookie);
    }

    /// <summary>Autentica o cliente como o navegador faria: Bearer token + cookie de fingerprint.</summary>
    public static void Authenticate(this HttpClient client, TestSession session) =>
        client.Authenticate(session.Token, session.FingerprintCookie);

    /// <summary>Autentica com token e cookie arbitrários (usado para simular ataques).</summary>
    public static void Authenticate(this HttpClient client, string token, string? fingerprintCookie)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("Cookie");
        if (fingerprintCookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", fingerprintCookie);
        }
    }
}
