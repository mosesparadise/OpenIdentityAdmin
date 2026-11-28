using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises the installed HTTPS issuer, protected key management and a real resource server using separate client and issuer keys.</summary>
[Collection("administrator-bootstrap")]
public sealed class KeyedMachineJourneyTests(BootstrapFixture fixture)
{
    private const string Slash = "/";
    private const string Client = "keyed-orders-indexer";
    private const string Display = "Order indexing service";
    private const string Registration = "/bff/api/v1/applications/machine-key-registrations";
    private const string Replacements = "/key-replacements";
    private const string TokenEndpoint = "/connect/token";
    private const string KeyProfile = "managed-machine-key-v1";
    private const string Access = "access_token";
    private const string Error = "error";
    private const int AssignedRevision = 2;

    /// <summary>Registers and rotates using the protected API, redeems two independent proofs and validates an access token at the sample API.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_KeyedMachine_When_ExchangingThenRotating_Then_OnlyFreshCurrentProofsReachTheApi(string provider)
    {
        var boundary = new KeyedPublicationDeadline();
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true, configureIssuer: boundary.Configure, clock: new FactorClock(boundary));
        using var initial = new NativeAssertionMaterial();
        using var replacement = new NativeAssertionMaterial();
        var expiry = http.Now.AddDays(1).ToString(ApplicationEditorRules.ExpiryFormat, CultureInfo.InvariantCulture);
        var create = new ApplicationKeyMachineCreate(new(Client, Display, expiry), PublicKey(initial));
        using var created = await http.CatalogueAsync(HttpMethod.Post, Registration, JsonSerializer.Serialize(create, JsonSerializerOptions.Web),
            direct: false, csrf: await http.TokenAsync());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var application = await created.Content.ReadFromJsonAsync<ApplicationDetails>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(application);
        Assert.Equal(KeyProfile, application.Profile);
        var test = new ManagedCacheTestContext(http, application.Id, Client, string.Empty, string.Empty);
        await ManagedConsentTest.SeedAsync(test, machine: true);
        var id = Guid.NewGuid().ToString();
        var signed = Sign(http, initial, id);
        await RequireIssuedAsync(http, signed, application.Id);
        await RequireInvalidAsync(http, signed);
        await RotateAsync(http, application, replacement, expiry);
        await RequireInvalidAsync(http, Sign(http, initial));
        await RequireInvalidAsync(http, Sign(http, replacement, id));
        await RequireIssuedAsync(http, Sign(http, replacement), application.Id);
        boundary.Arm();
        using var delayed = await ExchangeAsync(http, Sign(http, replacement));
        await ManagedCacheTestContext.UnavailableAsync(delayed);
        Assert.True(boundary.Advanced);
        await ManagedRetirementRaceTests.RetireAsync(test);
        await RequireInvalidAsync(http, Sign(http, replacement));
    }

    /// <summary>Recovers the current version from protected application details and compares the BFF read before rotating the key.</summary>
    private static async Task RotateAsync(ApplicationHttpFixture http, ApplicationDetails application, NativeAssertionMaterial replacement, string expiry)
    {
        var path = ApplicationHttpFixture.ApiCollection + Slash + Uri.EscapeDataString(application.Id);
        using var details = await http.CatalogueAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(ApplicationVersion.Create(application.Id, AssignedRevision), details.Headers.ETag?.Tag);
        using var browserDetails = await http.Browser.GetAsync(ApplicationHttpFixture.Collection + Slash + Uri.EscapeDataString(application.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, browserDetails.StatusCode);
        using var assigned = await http.CatalogueAsync(HttpMethod.Get, ApplicationScopeHttpTests.Path(application.Id, direct: true));
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var rotate = new ApplicationKeyReplacement(PublicKey(replacement), expiry, true);
        using var rotated = await http.CatalogueAsync(HttpMethod.Post, ApplicationHttpFixture.Collection + Slash + Uri.EscapeDataString(application.Id) + Replacements,
            JsonSerializer.Serialize(rotate, JsonSerializerOptions.Web), details.Headers.ETag!.Tag, direct: false, csrf: await http.TokenAsync());
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
    }

    /// <summary>Exports only the client's public key and signs bounded independent assertions against the actual configured issuer.</summary>
    private static ApplicationPublicKey PublicKey(NativeAssertionMaterial material)
    {
        var key = material.Policy.Key.CreateNativeKey();
        return new(key.Kty, key.Kid, key.Alg, key.Use, key.N, key.E);
    }

    /// <summary>Uses the current fixture time, the real canonical issuer and an optional retained request identifier.</summary>
    private static string Sign(ApplicationHttpFixture http, NativeAssertionMaterial material, string? id = null) =>
        material.Sign(http.Now.ToUnixTimeSeconds(), id, audience: http.IssuerClient.BaseAddress!.AbsoluteUri, clientId: Client);

    /// <summary>Uses the registered machine client ID and a single assigned resource scope over the actual token endpoint.</summary>
    private static async Task<HttpResponseMessage> ExchangeAsync(ApplicationHttpFixture http, string signed)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.ClientCredentials,
            [OpenIddictConstants.Parameters.ClientId] = Client,
            [OpenIddictConstants.Parameters.Scope] = ManagedConsentTest.ApiScope,
            [OpenIddictConstants.Parameters.ClientAssertionType] = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
            [OpenIddictConstants.Parameters.ClientAssertion] = signed
        });
        return await http.IssuerClient.PostAsync(TokenEndpoint, form, TestContext.Current.CancellationToken);
    }

    /// <summary>Requires the signed token to pass the sample resource server and its independent claims oracle.</summary>
    private static async Task RequireIssuedAsync(ApplicationHttpFixture http, string signed, string id)
    {
        using var result = await ExchangeAsync(http, signed);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var body = JsonDocument.Parse(await result.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        await MachineClientJourneyTests.VerifyAsync(http, body.RootElement.GetProperty(Access).GetString()!, id);
    }

    /// <summary>Confirms a formatted native authentication refusal after reuse or replacement.</summary>
    private static async Task RequireInvalidAsync(ApplicationHttpFixture http, string signed)
    {
        using var result = await ExchangeAsync(http, signed);
        Assert.True(result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await result.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, body.RootElement.GetProperty(Error).GetString());
    }
}
