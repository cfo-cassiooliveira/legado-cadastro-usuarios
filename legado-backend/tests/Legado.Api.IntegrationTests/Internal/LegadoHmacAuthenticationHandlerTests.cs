using System.Text;
using System.Text.Encodings.Web;
using Legado.Api.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Legado.Api.IntegrationTests.Internal;

/// <summary>
/// Handler HMAC isolado, para o que o TestServer não reproduz: ele bufferiza o corpo e sempre envia Content-Length,
/// então o corpo sem tamanho declarado (chunked) só é testável aqui.
/// </summary>
public sealed class LegadoHmacAuthenticationHandlerTests
{
    private const int MaxBodyBytes = 16 * 1024;
    private static readonly string Secret = Convert.ToBase64String(new byte[32]);

    // Regressão: sem Content-Length o corpo era lido inteiro (até o limite do Kestrel) antes de validar a assinatura.
    [Fact]
    public async Task Authenticate_WithUndeclaredBodyAboveTheLimit_RejectsReadingAtMostTheLimit()
    {
        var body = new CountingStream(1024 * 1024);
        var context = SignedContext(body, signedBody: []);

        var result = await AuthenticateAsync(context);

        result.Succeeded.ShouldBeFalse();
        body.BytesRead.ShouldBeLessThanOrEqualTo(MaxBodyBytes + 1);
    }

    [Fact]
    public async Task Authenticate_WithUndeclaredSmallBody_AcceptsAndRewindsForTheEndpoint()
    {
        var payload = Encoding.UTF8.GetBytes("{\"login\":\"chunked\"}");
        var context = SignedContext(new MemoryStream(payload), signedBody: payload);

        var result = await AuthenticateAsync(context);

        result.Succeeded.ShouldBeTrue();
        context.Request.Body.Position.ShouldBe(0);
    }

    private static DefaultHttpContext SignedContext(Stream body, byte[] signedBody)
    {
        const string path = "/api/v1/internal/user-lookups";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = HmacSignature.NewNonce();
        var signature = HmacSignature.Compute(Convert.FromBase64String(Secret), HmacSignature.Canonical(timestamp, nonce, "POST", path, signedBody));
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Body = body;
        context.Request.Headers[HmacSignature.HeaderName] = HmacSignature.Header("keycloak-1", timestamp, nonce, signature);
        return context;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(HttpContext context)
    {
        var schemeOptions = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions.Get(Arg.Any<string>()).Returns(new AuthenticationSchemeOptions());
        var internalApi = Options.Create(new InternalApiOptions
        {
            Enabled = true,
            Keys = { ["keycloak-1"] = new InternalApiKey { Caller = "keycloak", Secret = Secret } },
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new LegadoHmacAuthenticationHandler(
            schemeOptions, NullLoggerFactory.Instance, UrlEncoder.Default, internalApi, cache, TimeProvider.System);
        await handler.InitializeAsync(
            new AuthenticationScheme(LegadoHmacAuthenticationHandler.SchemeName, null, typeof(LegadoHmacAuthenticationHandler)), context);
        return await handler.AuthenticateAsync();
    }

    /// <summary>Corpo sem tamanho conhecido (não posicionável) que conta quanto foi lido.</summary>
    private sealed class CountingStream(long length) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toRead = (int)Math.Min(count, length - BytesRead);
            Array.Fill(buffer, (byte)'a', offset, toRead);
            BytesRead += toRead;
            return toRead;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var toRead = (int)Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..toRead].Fill((byte)'a');
            BytesRead += toRead;
            return ValueTask.FromResult(toRead);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
