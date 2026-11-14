using System.Text;
using System.Text.Json;
using Legado.Api.Internal;

namespace Legado.Api.IntegrationTests.Internal;

/// <summary>Formato da assinatura conferido contra vetores calculados com openssl (oráculo independente).</summary>
public sealed class HmacSignatureTests
{
    public static TheoryData<int> VectorIndexes => [0, 1, 2];

    [Theory]
    [MemberData(nameof(VectorIndexes))]
    public void Compute_MatchesTheSharedTestVectors(int index)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Internal", "hmac-test-vectors.json")));
        var key = Convert.FromBase64String(vectors.RootElement.GetProperty("secret").GetString()!);
        var vector = vectors.RootElement.GetProperty("vectors")[index];

        var canonical = HmacSignature.Canonical(
            vector.GetProperty("timestamp").GetInt64(),
            vector.GetProperty("nonce").GetString()!,
            vector.GetProperty("method").GetString()!,
            vector.GetProperty("pathAndQuery").GetString()!,
            Encoding.UTF8.GetBytes(vector.GetProperty("body").GetString()!));

        canonical.ShouldEndWith(vector.GetProperty("bodySha256").GetString()!);
        Convert.ToBase64String(HmacSignature.Compute(key, canonical)).ShouldBe(vector.GetProperty("signature").GetString());
    }

    [Fact]
    public void Header_RoundTripsThroughTryParse()
    {
        var nonce = HmacSignature.NewNonce();
        var header = HmacSignature.Header("keycloak-1", 1763035200, nonce, [1, 2, 3]);

        var parsed = HmacSignature.TryParse(header).ShouldNotBeNull();
        parsed.KeyId.ShouldBe("keycloak-1");
        parsed.Timestamp.ShouldBe(1763035200);
        parsed.Nonce.ShouldBe(nonce);
        parsed.Signature.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("keyId=k, t=1, n=AAECAwQFBgcICQoLDA0ODw")]
    [InlineData("keyId=k, t=abc, n=AAECAwQFBgcICQoLDA0ODw, v1=AQID")]
    [InlineData("keyId=k, t=-5, n=AAECAwQFBgcICQoLDA0ODw, v1=AQID")]
    [InlineData("keyId=k, t=1, n=AAECAwQFBgcICQoLDA0ODw, v1=not-base64!")]
    [InlineData("t=1, n=AAECAwQFBgcICQoLDA0ODw, v1=AQID")]
    [InlineData("keyId=k, t=1, v1=AQID")]
    [InlineData("keyId=k, t=1, n=curto, v1=AQID")]
    [InlineData("keyId=k, t=1, n=AAECAwQFBgcICQoLDA0OD+/, v1=AQID")]
    public void TryParse_RejectsMalformedHeaders(string? header) =>
        HmacSignature.TryParse(header).ShouldBeNull();

    [Fact]
    public void NewNonce_IsUniqueAndAccepted()
    {
        var first = HmacSignature.NewNonce();

        first.ShouldNotBe(HmacSignature.NewNonce());
        HmacSignature.TryParse(HmacSignature.Header("k", 1, first, [1])).ShouldNotBeNull();
    }
}
