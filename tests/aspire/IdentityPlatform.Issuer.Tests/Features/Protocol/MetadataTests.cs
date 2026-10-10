using System.Net;
using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class MetadataTests(IssuerFixture fixture)
{
    private static readonly string[] PrivateFields = ["d", "p", "q", "dp", "dq", "qi", "k"];

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Discovery_and_jwks_advertise_only_the_implemented_profile_and_public_keys(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var client = new HttpClient { BaseAddress = environment.Issuer };
        using var metadata = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration", TestContext.Current.CancellationToken));
        var root = metadata.RootElement;
        Assert.Equal(environment.Issuer.AbsoluteUri, root.GetProperty("issuer").GetString());
        Assert.Equal(["authorization_code"], Strings(root, "grant_types_supported"));
        Assert.Equal(["code"], Strings(root, "response_types_supported"));
        Assert.Equal(["query"], Strings(root, "response_modes_supported"));
        Assert.Equal(["S256"], Strings(root, "code_challenge_methods_supported"));
        Assert.Equal(["client_secret_basic"], Strings(root, "token_endpoint_auth_methods_supported"));
        Assert.False(root.TryGetProperty("userinfo_endpoint", out _));
        Assert.False(root.TryGetProperty("end_session_endpoint", out _));
        Assert.False(root.TryGetProperty("pushed_authorization_request_endpoint", out _));
        using var jwks = JsonDocument.Parse(await client.GetStringAsync("/connect/jwks", TestContext.Current.CancellationToken));
        var keys = jwks.RootElement.GetProperty("keys").EnumerateArray().ToArray();
        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.Equal("sig", key.GetProperty("use").GetString());
            Assert.Equal("RSA", key.GetProperty("kty").GetString());
            Assert.All(PrivateFields, name => Assert.False(key.TryGetProperty(name, out _)));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/clients", TestContext.Current.CancellationToken)).StatusCode);
    }

    private static string?[] Strings(JsonElement element, string name) => element.GetProperty(name).EnumerateArray().Select(value => value.GetString()).ToArray();
}
