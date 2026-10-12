using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal static class KeyEvidence
{
    private static readonly string[] PrivateFields = ["d", "p", "q", "dp", "dq", "qi", "oth", "k"];

    public static async Task<string> ImmutableEntryAsync(IssuerEnvironment host, string reference)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByReferenceIdAsync(reference, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Id = await manager.GetIdAsync(entry, TestContext.Current.CancellationToken),
            Created = await manager.GetCreationDateAsync(entry, TestContext.Current.CancellationToken),
            Expires = await manager.GetExpirationDateAsync(entry, TestContext.Current.CancellationToken),
            Payload = await manager.GetPayloadAsync(entry, TestContext.Current.CancellationToken),
            Application = await manager.GetApplicationIdAsync(entry, TestContext.Current.CancellationToken),
            Authorization = await manager.GetAuthorizationIdAsync(entry, TestContext.Current.CancellationToken),
            Subject = await manager.GetSubjectAsync(entry, TestContext.Current.CancellationToken)
        });
        try
        {
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    public static async Task<string> PublicKeysAsync(IssuerEnvironment host, string node, params string[] expected)
    {
        using var http = host.NodeClient(node);
        using var response = await http.GetAsync(new Uri(host.Issuer, "/connect/jwks"), TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(response.Headers.Pragma, value => value.Name == "no-cache");
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var keys = new JsonWebKeySet(json);
        Assert.Equal(expected.Order(StringComparer.Ordinal), keys.Keys.Select(key => key.Kid).Order(StringComparer.Ordinal));
        Assert.True(keys.Keys.All(key => !key.HasPrivateKey), "JWKS contains private material.");
        using var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("keys").EnumerateArray().All(key =>
            PrivateFields.All(field => !key.TryGetProperty(field, out _))), "JWKS contains private fields.");
        return json;
    }

    public static async Task RequirePublicAsync(KeyRotationEnvironment fixture, string node, TestFlow flow, JsonElement tokens,
        string signing, string? frozen = null, bool valid = true)
    {
        using var transport = fixture.Host.NodeClient(node);
        var result = await fixture.Host.RelyingParty.ValidateAsync(tokens, flow, TestContext.Current.CancellationToken, transport, frozen);
        Assert.True(valid ? result.Length == 0 : result.StartsWith("Independent JWT validation failed:", StringComparison.Ordinal),
            "Frozen/current public verification produced an unexpected decision.");
        foreach (var name in new[] { "access_token", "id_token" })
        {
            var token = new JsonWebToken(tokens.GetProperty(name).GetString()!);
            Assert.Equal(signing, token.Kid);
        }
    }

    public static async Task<DateTimeOffset> RequirePayloadAsync(KeyRotationEnvironment fixture, string reference, string type,
        string signing, string encryption)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByReferenceIdAsync(reference, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.True(await manager.GetTypeAsync(entry, TestContext.Current.CancellationToken) == type, "Wrong native token entry type.");
        var payload = await manager.GetPayloadAsync(entry, TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrEmpty(payload), "Native reference token lacks its protected payload.");
        var outer = new JsonWebToken(payload!);
        Assert.True(outer.IsEncrypted && outer.Kid == encryption, "Native encryption selection differs from the active credential.");
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(payload, new TokenValidationParameters
        {
            ValidIssuer = fixture.Host.Issuer.AbsoluteUri,
            ValidateIssuer = true,
            ValidateAudience = false, // Private protocol artifacts have native presenter binding, not public resource audiences.
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512],
            IssuerSigningKeys = new[] { fixture.Original.Signing, fixture.Next.Signing }.Select(certificate => new X509SecurityKey(certificate)),
            TokenDecryptionKeys = new[] { fixture.Original.Encryption, fixture.Next.Encryption }.Select(certificate => new X509SecurityKey(certificate)),
            ValidTypes = [type == TokenTypeIdentifiers.Private.AuthorizationCode ? JsonWebTokenTypes.Private.AuthorizationCode : JsonWebTokenTypes.Private.RequestToken]
        });
        Assert.True(result.IsValid && result.SecurityToken is JsonWebToken, "Native protected payload failed independent verification.");
        var token = (JsonWebToken)result.SecurityToken;
        var inner = token.InnerToken ?? token;
        Assert.True(inner.Kid == signing, "Native inner signature selection differs from the active credential.");
        var expires = await manager.GetExpirationDateAsync(entry, TestContext.Current.CancellationToken);
        Assert.NotNull(expires);
        Assert.True(expires == new DateTimeOffset(inner.ValidTo), "Persisted/native payload expiry mismatch.");
        Assert.True(expires > DateTimeOffset.UtcNow, "Artifact expired before its acceptance control.");
        return expires.Value;
    }
}
