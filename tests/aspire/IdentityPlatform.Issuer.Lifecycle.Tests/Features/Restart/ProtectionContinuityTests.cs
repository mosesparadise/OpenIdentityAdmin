using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart;

[Collection("lifecycle")]
public sealed class ProtectionContinuityTests(LifecycleFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Original_tokens_validate_with_fresh_public_keys_and_new_tokens_with_retained_keys(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        using var original = await CodeContinuityTests.RedeemAsync(environment, evidence);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var retained = new JsonWebKeySet(await http.GetStringAsync(new Uri(environment.Issuer, "/connect/jwks"), TestContext.Current.CancellationToken));
        await environment.RestartIssuerAsync();
        var current = new JsonWebKeySet(await http.GetStringAsync(new Uri(environment.Issuer, "/connect/jwks"), TestContext.Current.CancellationToken));
        Assert.True(PublicKeys(retained).SetEquals(PublicKeys(current)), "Restart changed public signing material.");
        var privateControl = new JsonWebKeySet();
        var publicKey = current.Keys[0];
        privateControl.Keys.Add(new JsonWebKey { Kty = "RSA", N = publicKey.N, E = publicKey.E, Oth = { "synthetic-private-component" } });
        Assert.ThrowsAny<Exception>(() => PublicKeys(privateControl));
        Assert.True((await environment.RelyingParty.ValidateAsync(original.RootElement, evidence.Flow, TestContext.Current.CancellationToken)).Length == 0,
            "Original token bytes failed fresh metadata/public-key validation.");
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        var freshEvidence = await ExchangeEvidence.IssueAsync(environment, recovery);
        using var fresh = await CodeContinuityTests.RedeemAsync(environment, freshEvidence);
        var handler = new JsonWebTokenHandler();
        using var wrongRsa = RSA.Create(3072);
        foreach (var property in new[] { "id_token", "access_token" })
        {
            var audience = property == "id_token" ? "identityplatform-pilot" : "identityplatform-pilot-api";
            var type = property == "id_token" ? "JWT" : "at+jwt";
            var token = fresh.RootElement.GetProperty(property).GetString()!;
            Assert.True((await handler.ValidateTokenAsync(token, TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, audience, type, retained.GetSigningKeys()))).IsValid,
                "New token failed validation with retained pre-restart public keys.");
            var segments = token.Split('.');
            segments[2] = (segments[2][0] == 'A' ? "B" : "A") + segments[2][1..];
            Assert.False((await handler.ValidateTokenAsync(string.Join('.', segments), TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, audience, type, current.GetSigningKeys()))).IsValid,
                "Tampered signature passed public validation.");
            Assert.False((await handler.ValidateTokenAsync(token, TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, audience, type, [new RsaSecurityKey(wrongRsa)]))).IsValid,
                "Unrelated signing key passed validation.");
        }
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Populated_ring_rejects_unrelated_wrapper_then_original_cookie_recovers(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        await environment.RestartIssuerAsync(environment.RejectUnrelatedWrapperAsync);
        await RestartEvidence.SilentAsync(environment, cookies, original, allowed: true);
    }

    private static HashSet<string> PublicKeys(JsonWebKeySet keys)
    {
        Assert.NotEmpty(keys.Keys);
        Assert.True(keys.Keys.All(key => key.Kty == "RSA" && string.IsNullOrEmpty(key.D) && string.IsNullOrEmpty(key.P) && string.IsNullOrEmpty(key.Q) &&
            string.IsNullOrEmpty(key.DP) && string.IsNullOrEmpty(key.DQ) && string.IsNullOrEmpty(key.QI) && string.IsNullOrEmpty(key.K) && key.Oth.Count == 0 && !string.IsNullOrEmpty(key.N) && !string.IsNullOrEmpty(key.E)),
            "JWKS must contain public RSA signing material only.");
        return keys.Keys.Select(key => JsonSerializer.Serialize(new { key.Kid, key.Kty, key.Alg, key.Use, key.N, key.E, key.X5c })).ToHashSet(StringComparer.Ordinal);
    }
}
