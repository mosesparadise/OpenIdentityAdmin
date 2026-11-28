using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Verifies real native client authentication and a signed application-only access token after protected API assignment.</summary>
[Collection("administrator-bootstrap")]
public sealed class MachineClientJourneyTests(BootstrapFixture fixture)
{
    private const string Client = "orders-machine-client";
    private const string Display = "Order indexing service";
    private const string Scope = "orders.read";
    private const string Audience = "orders-api";
    private const string Discovery = "/.well-known/openid-configuration";
    private const string Endpoint = "/connect/token";
    private const string ScopeField = "scope";
    private const string GrantField = "grant_type";
    private const string Grant = "client_credentials";
    private const string Access = "access_token";

    /// <summary>Rejects unassigned scopes and wrong credentials, verifies signed claims, and stops issuance when the registration is disabled.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ConfidentialMachine_When_ExchangingAssignedScope_Then_OnlyItsApiAcceptsTheServiceIdentity(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var body = JsonSerializer.Serialize(new ApplicationMachineCreate(Client, Display, http.Now.AddDays(1).ToString(ApplicationEditorRules.ExpiryFormat, System.Globalization.CultureInfo.InvariantCulture)), JsonSerializerOptions.Web);
        using var created = await http.CreateAsync(body, machine: true);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var service = await created.Content.ReadFromJsonAsync<ApplicationCredentialOutput>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(service);
        Assert.Equal("managed-machine-v1", service.Application.Profile);
        Assert.Empty(service.Application.RedirectUris);
        var test = new ManagedCacheTestContext(http, service.Application.Id, Client, string.Empty, service.ClientSecret);
        using var denied = await TokenAsync(http, Scope, service.ClientSecret);
        Assert.False(denied.IsSuccessStatusCode);
        await ManagedConsentTest.SeedAsync(test, machine: true);
        using var wrong = await TokenAsync(http, Scope, "unused-invalid-secret");
        Assert.False(wrong.IsSuccessStatusCode);
        using var identity = await TokenAsync(http, "openid", service.ClientSecret);
        Assert.False(identity.IsSuccessStatusCode);
        using var response = await TokenAsync(http, Scope, service.ClientSecret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(tokens.RootElement.TryGetProperty("id_token", out _));
        Assert.False(tokens.RootElement.TryGetProperty("refresh_token", out _));
        await VerifyAsync(http, tokens.RootElement.GetProperty(Access).GetString()!, service.Application.Id);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, enabled: false, revision: 2);
        using var disabled = await TokenAsync(http, Scope, service.ClientSecret);
        Assert.False(disabled.IsSuccessStatusCode);
    }

    /// <summary>Authenticates the service through HTTPS Basic on an exact client-credentials request.</summary>
    private static async Task<HttpResponseMessage> TokenAsync(ApplicationHttpFixture http, string scope, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Client + ":" + Uri.EscapeDataString(secret))));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { [GrantField] = Grant, [ScopeField] = scope });
        return await http.IssuerClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the signature with the issuer's discovered public keys, expected audience, lifetime and nonhuman subject.</summary>
    private static async Task VerifyAsync(ApplicationHttpFixture http, string token, string id)
    {
        var issuer = http.IssuerClient.BaseAddress!;
        using var discovery = await http.IssuerClient.GetFromJsonAsync<JsonDocument>(Discovery, TestContext.Current.CancellationToken);
        Assert.NotNull(discovery);
        var keys = new Uri(discovery.RootElement.GetProperty("jwks_uri").GetString()!);
        Assert.Equal(issuer.Authority, keys.Authority);
        var signing = new JsonWebKeySet(await http.IssuerClient.GetStringAsync(keys, TestContext.Current.CancellationToken));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = discovery.RootElement.GetProperty("issuer").GetString(),
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signing.GetSigningKeys(),
            ValidTypes = ["at+jwt"]
        };
        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var result = await handler.ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid);
        Assert.Equal("machine", result.ClaimsIdentity.FindFirst("identity_type")?.Value);
        Assert.Equal("machine:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id))), result.ClaimsIdentity.FindFirst("sub")?.Value);
        Assert.Equal(Scope, result.ClaimsIdentity.FindFirst(ScopeField)?.Value);
        Assert.Null(result.ClaimsIdentity.FindFirst("email"));
        parameters.ValidAudience = "identity-admin-api";
        Assert.False((await handler.ValidateTokenAsync(token, parameters)).IsValid);
    }
}
