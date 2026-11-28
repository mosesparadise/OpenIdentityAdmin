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
    private const string InvoiceAudience = "invoices-api";
    private const string Discovery = "/.well-known/openid-configuration";
    private const string Endpoint = "/connect/token";
    private const string ScopeSeparator = " ";
    private const string ScopeField = "scope";
    private const string GrantField = "grant_type";
    private const string Grant = "client_credentials";
    private const string Error = "error";
    private const string InvalidRequest = "invalid_request";
    private const string InvalidClient = "invalid_client";
    private const string Machine = "machine";
    private const int RotatedRevision = 3;
    private const string InvalidScope = "invalid_scope";
    private const string Access = "access_token";

    private const int AssignedRevision = 2;
    private const int ClearedRevision = 4;
    private const int ReassignedRevision = 5;
    private const string IncorrectCredential = "unused-invalid-secret";
    private const string OpenId = "openid";
    private const string Basic = "Basic";
    private const string Separator = ":";
    private const string Issuer = "issuer";
    private const string KeySet = "jwks_uri";
    private const string AccessType = "at+jwt";
    private const string AdminAudience = "identity-admin-api";

    private const string IdentityToken = "id_token";
    private const string RefreshToken = "refresh_token";
    private const string IdentityType = "identity_type";
    private const string SubjectPrefix = "machine:";
    private const string Subject = "sub";
    private const string Email = "email";
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
        await RequireErrorAsync(denied, HttpStatusCode.BadRequest, InvalidScope);
        var scopeId = await ManagedConsentTest.SeedAsync(test, machine: true);
        await RequireAudienceSelectionAsync(http, test.Id, service.ClientSecret);
        using var wrong = await TokenAsync(http, Scope, IncorrectCredential);
        await RequireErrorAsync(wrong, HttpStatusCode.Unauthorized, InvalidClient);
        using var identity = await TokenAsync(http, OpenId, service.ClientSecret);
        await RequireErrorAsync(identity, HttpStatusCode.BadRequest, InvalidRequest);
        using var response = await TokenAsync(http, Scope, service.ClientSecret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(tokens.RootElement.TryGetProperty(IdentityToken, out _));
        Assert.False(tokens.RootElement.TryGetProperty(RefreshToken, out _));
        await VerifyAsync(http, tokens.RootElement.GetProperty(Access).GetString()!, service.Application.Id);
        var replacement = await RotateAsync(http, service);
        await RequireRetainedScopeAsync(test, scopeId);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, enabled: false, revision: ReassignedRevision);
        using var disabled = await TokenAsync(http, Scope, replacement);
        await RequireErrorAsync(disabled, HttpStatusCode.Unauthorized, InvalidClient);
    }

    /// <summary>Checks separate signed tokens for distinct audiences and the issuer's refusal of a combined request.</summary>
    private static async Task RequireAudienceSelectionAsync(ApplicationHttpFixture http, string id, string secret)
    {
        using var invoice = await TokenAsync(http, ManagedConsentTest.OtherApiScope, secret);
        Assert.Equal(HttpStatusCode.OK, invoice.StatusCode);
        using var invoiceBody = JsonDocument.Parse(await invoice.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        await VerifyAsync(http, invoiceBody.RootElement.GetProperty(Access).GetString()!, id, InvoiceAudience, ManagedConsentTest.OtherApiScope);
        using var both = await TokenAsync(http, Scope + ScopeSeparator + ManagedConsentTest.OtherApiScope, secret);
        await RequireErrorAsync(both, HttpStatusCode.BadRequest, InvalidScope);
    }

    /// <summary>Removes the live application assignment and observes the original issued token's retained scope through the protected API.</summary>
    private static async Task RequireRetainedScopeAsync(ManagedCacheTestContext test, string scopeId)
    {
        const string Scopes = "/api/v1/scopes/";
        const string Referenced = "referenced";
        using var clear = await test.Http.CatalogueAsync(HttpMethod.Put, ApplicationScopeHttpTests.Path(test.Id), ApplicationScopeHttpTests.Clear,
            ApplicationVersion.Create(test.Id, RotatedRevision), direct: false, csrf: await test.Http.TokenAsync());
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        using var response = await test.Http.CatalogueAsync(HttpMethod.Get, Scopes + Uri.EscapeDataString(scopeId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty(Referenced).GetBoolean());
        const string OrdersOnly = """{"scopes":["orders.read"]}""";
        using var assign = await test.Http.CatalogueAsync(HttpMethod.Put, ApplicationScopeHttpTests.Path(test.Id), OrdersOnly,
            ApplicationVersion.Create(test.Id, ClearedRevision), direct: false, csrf: await test.Http.TokenAsync());
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
    }

    /// <summary>Rotates the secret through the BFF and exercises both credentials against the native token endpoint.</summary>
    private static async Task<string> RotateAsync(ApplicationHttpFixture http, ApplicationCredentialOutput original)
    {
        using var response = await http.UpdateAsync(original.Application.Id, ApplicationHttpUpdateEvidence.Replacement(http.Now),
            ApplicationVersion.Create(original.Application.Id, AssignedRevision), replacement: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await response.Content.ReadFromJsonAsync<ApplicationCredentialOutput>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(rotated);
        Assert.NotEqual(original.ClientSecret, rotated.ClientSecret);
        using var previous = await TokenAsync(http, Scope, original.ClientSecret);
        await RequireErrorAsync(previous, HttpStatusCode.Unauthorized, InvalidClient);
        using var current = await TokenAsync(http, Scope, rotated.ClientSecret);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        return rotated.ClientSecret;
    }

    /// <summary>Requires a formatted OAuth error so infrastructure or routing failures cannot pass as a policy denial.</summary>
    private static async Task RequireErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(error, body.RootElement.GetProperty(Error).GetString());
    }

    /// <summary>Authenticates the service through HTTPS Basic on an exact client-credentials request.</summary>
    private static async Task<HttpResponseMessage> TokenAsync(ApplicationHttpFixture http, string scope, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(Basic, Convert.ToBase64String(Encoding.UTF8.GetBytes(Client + Separator + Uri.EscapeDataString(secret))));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { [GrantField] = Grant, [ScopeField] = scope });
        return await http.IssuerClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the signature with the issuer's discovered public keys, expected audience, lifetime and nonhuman subject.</summary>
    internal static async Task VerifyAsync(ApplicationHttpFixture http, string token, string id, string audience = Audience, string scope = Scope)
    {
        var issuer = http.IssuerClient.BaseAddress!;
        using var discovery = await http.IssuerClient.GetFromJsonAsync<JsonDocument>(Discovery, TestContext.Current.CancellationToken);
        Assert.NotNull(discovery);
        var keys = new Uri(discovery.RootElement.GetProperty(KeySet).GetString()!);
        Assert.Equal(issuer.Authority, keys.Authority);
        var signing = new JsonWebKeySet(await http.IssuerClient.GetStringAsync(keys, TestContext.Current.CancellationToken));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = discovery.RootElement.GetProperty(Issuer).GetString(),
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signing.GetSigningKeys(),
            ValidTypes = [AccessType]
        };
        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var result = await handler.ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid);
        Assert.Equal(Machine, result.ClaimsIdentity.FindFirst(IdentityType)?.Value);
        Assert.Equal(SubjectPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id))), result.ClaimsIdentity.FindFirst(Subject)?.Value);
        Assert.Equal(scope, result.ClaimsIdentity.FindFirst(ScopeField)?.Value);
        Assert.Equal(audience, Assert.Single(((JsonWebToken)result.SecurityToken).Audiences));
        Assert.Null(result.ClaimsIdentity.FindFirst(Email));
        if (audience == Audience)
        {
            await MachineResourceTest.RequireAsync(token, parameters.ValidIssuer!, signing.GetSigningKeys());
        }
        parameters.ValidAudience = AdminAudience;
        Assert.False((await handler.ValidateTokenAsync(token, parameters)).IsValid);
    }
}
