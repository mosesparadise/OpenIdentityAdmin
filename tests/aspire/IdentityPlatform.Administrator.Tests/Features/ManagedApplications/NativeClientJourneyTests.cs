using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises a public native registration through the real issuer, fresh human consent, S256 exchange and token verification.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientJourneyTests(BootstrapFixture fixture)
{
    private const string Client = "native-application-link";
    private const string Callback = "https://mobile.example.test/callback";
    private const string NativeProfile = "managed-native-v1";
    private static readonly string[] Redirects = [Callback];
    private const string Display = "Native application link";
    private const int AssignedRevision = 2;
    private const int LifecycleTransitions = 2;
    private const string RequestedScopes = "openid orders.read";
    private const string EmailScope = "email";
    private const string ScopeField = "scope";
    private const string AccessTokenField = "access_token";
    private const string IncorrectSecret = "unused-native-client-secret";

    /// <summary>Requires a strong native code verifier and validates the resulting user-bound tokens without a client secret.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PublicNativeApplication_When_S256AndConsentComplete_Then_SecretlessExchangeReturnsBoundTokens(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var registration = await RegisterAsync(http);
        var flow = new ManagedTokenFlow(registration);
        await flow.ApproveAsync();
        var code = await flow.CodeAsync();
        using var wrong = await flow.ExchangeAsync(code, secret: IncorrectSecret);
        Assert.True(wrong.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
        var before = await ManagedHumanEvidence.DigestAsync(registration);
        await ManagedTokenFailureTests.RequireBoundExchangeAsync(registration, flow, code, before);
        await ManagedJourneyEvidence.ExchangeAsync(registration, flow, code, denyManagement: true);
        await VerifyScopeAssignmentAsync(registration);
    }

    /// <summary>Assigns scopes over the protected browser surface and verifies only the consented API scope and audience reach the native client.</summary>
    private static async Task VerifyScopeAssignmentAsync(ManagedCacheTestContext registration)
    {
        var http = registration.Http;
        var path = ApplicationScopeHttpTests.Path(registration.Id);
        using var original = await http.CatalogueAsync(HttpMethod.Get, path, direct: false);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var empty = await original.Content.ReadFromJsonAsync<ApplicationScopes>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(empty);
        Assert.Equal(registration.Id, empty.ApplicationId);
        Assert.Empty(empty.Scopes);
        Assert.Equal(ApplicationVersion.Create(registration.Id, 1), original.Headers.ETag?.ToString());
        using var replacement = await http.UpdateAsync(registration.Id, ApplicationHttpUpdateEvidence.Replacement(http.Now), original.Headers.ETag!.ToString(), replacement: true);
        Assert.Equal(HttpStatusCode.Conflict, replacement.StatusCode);

        await ManagedConsentTest.SeedAsync(registration);
        using var assigned = await http.CatalogueAsync(HttpMethod.Get, path, direct: false);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var details = await assigned.Content.ReadFromJsonAsync<ApplicationScopes>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(registration.Id, details.ApplicationId);
        Assert.Equal([EmailScope, ManagedConsentTest.ApiScope], details.Scopes);
        Assert.Equal(ApplicationVersion.Create(registration.Id, AssignedRevision), assigned.Headers.ETag?.ToString());
        await ManagedLifecycleJourneyTests.TransitionAsync(registration, false, AssignedRevision);
        using var disabled = await registration.SendAsync(NativeCacheFlow.Initial(NativeCacheProfile.Random(), registration.ClientIdentifier, registration.CallbackUri));
        await ManagedCacheTestContext.UnavailableAsync(disabled);
        await ManagedLifecycleJourneyTests.TransitionAsync(registration, true, AssignedRevision + 1);
        await VerifyConsentedTokensAsync(registration);
    }

    /// <summary>Verifies the scoped token response and UserInfo against the account that signs in and its available email.</summary>
    private static async Task VerifyConsentedTokensAsync(ManagedCacheTestContext registration)
    {
        var subject = await ManagedConsentTest.EmailAsync(registration, ManagedConsentTest.Address, verified: true);
        var flow = new ManagedTokenFlow(registration, RequestedScopes);
        await flow.ApproveAsync();
        using var response = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(RequestedScopes, body.RootElement.GetProperty(ScopeField).GetString());
        var row = Assert.Single((await registration.SnapshotAsync(applicationOnly: true)).Interactions, value => value.RequestId == flow.RequestId);
        Assert.Equal(subject, row.UserId);
        var projection = new ManagedTokenProjection(RequestedScopes, ManagedConsentTest.Resource);
        await ManagedTokenOracle.ValidateAsync(registration, flow, body.RootElement, subject, applicationOnly: true, multipleFlows: true, projection: projection);
        var access = body.RootElement.GetProperty(AccessTokenField).GetString()!;
        using var userInfo = await ManagedConsentTest.ReadAsync(registration, access);
        await ManagedConsentTest.RequireAsync(userInfo, subject, null, false);
        var retirement = await ManagedRetirementRaceTests.RetireAsync(registration, LifecycleTransitions);
        await ManagedTokenOracle.ValidateAsync(registration, flow, body.RootElement, subject, applicationOnly: true, multipleFlows: true, projection: projection);
        await ManagedRetirementRaceTests.RequireIssuerDenialAsync(registration, access);
        await GrantIssuerRevocation.ReconcileAsync(registration, retirement.Job);
    }

    /// <summary>Registers through the protected BFF and binds the returned native client identity to the exact submitted settings.</summary>
    private static async Task<ManagedCacheTestContext> RegisterAsync(ApplicationHttpFixture http)
    {
        var body = JsonSerializer.Serialize(new
        {
            clientId = Client,
            displayName = Display,
            redirectUris = Redirects
        });
        using var response = await http.CreateAsync(body, native: true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<ApplicationDetails>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(NativeProfile, details.Profile);
        Assert.Equal(Client, details.ClientId);
        Assert.Equal(Callback, Assert.Single(details.RedirectUris));
        Assert.Null(details.Credential);
        return new(http, details.Id, Client, Callback, null);
    }
}
