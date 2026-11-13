using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Api.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Legado.Api.IntegrationTests.Internal;

/// <summary>
/// Host da API com a API interna ligada. No TestServer a conexão não tem porta (0): a porta interna é configurada como
/// 0 para as rotas internas responderem; as rotas públicas continuam no host padrão do <see cref="LegadoApiFactory"/>.
/// </summary>
public sealed class InternalApiHost : IAsyncDisposable
{
    /// <summary>Chave do Keycloak gerada a cada execução (nenhum segredo versionado).</summary>
    public static readonly string KeycloakSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>Chave do Cadastro gerada a cada execução.</summary>
    public static readonly string CadastroSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly WebApplicationFactory<Program> _host;

    /// <summary>Sobe o host com a API interna ligada na porta informada.</summary>
    public InternalApiHost(LegadoApiFactory factory, int internalPort = 0, Action<IWebHostBuilder>? configure = null)
    {
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("InternalApi:Enabled", "true");
            builder.UseSetting("InternalApi:Port", internalPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("InternalApi:Keys:keycloak-1:Caller", "keycloak");
            builder.UseSetting("InternalApi:Keys:keycloak-1:Secret", KeycloakSecret);
            builder.UseSetting("InternalApi:Keys:cadastro-1:Caller", "cadastro");
            builder.UseSetting("InternalApi:Keys:cadastro-1:Secret", CadastroSecret);
            configure?.Invoke(builder);
        });
        Client = _host.CreateClient();
    }

    /// <summary>Cliente do host com a API interna.</summary>
    public HttpClient Client { get; }

    /// <summary>Envia uma chamada assinada como o Keycloak (padrão) ou outro chamador.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string pathAndQuery, object? body = null,
        string keyId = "keycloak-1", string? secret = null, long? timestamp = null, string? nonce = null,
        Action<HttpRequestMessage>? tamper = null)
    {
        var json = body is null ? string.Empty : JsonSerializer.Serialize(body, JsonSerializerOptions.Web);
        var request = new HttpRequestMessage(method, pathAndQuery);
        if (body is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var t = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var n = nonce ?? HmacSignature.NewNonce();
        var key = Convert.FromBase64String(secret ?? (keyId.StartsWith("cadastro", StringComparison.Ordinal) ? CadastroSecret : KeycloakSecret));
        var signature = HmacSignature.Compute(key, HmacSignature.Canonical(t, n, method.Method, pathAndQuery, Encoding.UTF8.GetBytes(json)));
        request.Headers.Add(HmacSignature.HeaderName, HmacSignature.Header(keyId, t, n, signature));
        tamper?.Invoke(request);
        return Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Atalho: POST assinado e leitura do JSON de resposta.</summary>
    public async Task<JsonElement> PostJsonAsync(string path, object body, string keyId = "keycloak-1")
    {
        using var response = await SendAsync(HttpMethod.Post, path, body, keyId);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.DisposeAsync();
    }
}
