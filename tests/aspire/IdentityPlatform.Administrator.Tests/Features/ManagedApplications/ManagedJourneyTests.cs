using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Closes the real created-client journey boundaries left after native issuance, with no synthetic human or credential shortcut.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedJourneyTests(BootstrapFixture fixture)
{
    private const string SecondClient = "second-managed-native-client";
    private const string SecondCallback = "https://second-managed.example.test/callback";
    private const string ClientId = "client_id";
    private const string InvalidGrant = "invalid_grant";
    private const string InvalidToken = "invalid_token";
    private const string Valid = "valid";
    private const string Redeemed = "redeemed";
    private const string Access = "urn:ietf:params:oauth:token-type:access_token";
    private const string Identity = "urn:ietf:params:oauth:token-type:id_token";
    private const int IssuedCredentials = 2;

    /// <summary>Actual cancellation leaves the request unspent but terminal; retrying its original authorize URI cannot reopen authentication or issue credentials.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExplicitlyCancelledManagedConsent_When_OriginalAuthorizationRequestIsRetried_Then_TerminalStateAndAllAuthorityRemainUnchanged(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.CacheAsync();
        await flow.DecideAsync(approve: false);
        var cancelled = await test.SnapshotAsync();
        Assert.Equal(ManagedLoginState.Denied, Assert.Single(cancelled.Interactions).State);
        Assert.True(Assert.Single(cancelled.Tokens).Status == Valid);
        Assert.Equal(0, cancelled.Authorizations);
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var replay = await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(replay);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
    }

    /// <summary>A successfully consumed cached request cannot issue a second code or change the already-issued native credentials, audit or original deadlines.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompletedManagedCodeAndTokenIssuance_When_OriginalCachedRequestIsReplayed_Then_OnlyLocalRejectionOccursAndIssuedCredentialsRemainValid(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        await ManagedJourneyEvidence.ExchangeAsync(test, flow, await flow.CodeAsync());
        var issued = await test.SnapshotAsync();
        var interaction = Assert.Single(issued.Interactions);
        Assert.Equal(ManagedLoginState.Consumed, interaction.State);
        Assert.True(issued.Tokens.Single(row => row.Id == interaction.RequestId).Status == Redeemed);
        Assert.Equal(IssuedCredentials, issued.Tokens.Count(row => row.Type is Access or Identity && row.Status == Valid));
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var replay = await test.SendAsync(flow.Resume);
        await ManagedTokenEvidence.ErrorAsync(replay, HttpStatusCode.BadRequest, InvalidToken, local: true);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
    }

    /// <summary>Two independently API-created registrations cannot exchange protected requests or codes; each original proof still completes its own verified flow.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TwoGenuinelyCreatedManagedClients_When_CachedRequestsAndAuthenticatedCodesAreSubstituted_Then_BothOriginalClientBindingsRemainUsable(string provider)
    {
        await using var first = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        using var created = await first.Http.CreateAsync(ApplicationHttpTests.Input(first.Http.Now, SecondClient, SecondCallback));
        var second = new ManagedCacheTestContext(first.Http, await ApplicationHttpTests.CreatedAsync(created));
        Assert.Equal(SecondClient, second.ClientIdentifier);
        Assert.Equal(SecondCallback, second.CallbackUri);
        Assert.True(first.Id != second.Id && first.ClientIdentifier != second.ClientIdentifier && first.CallbackUri != second.CallbackUri && first.Secret != second.Secret);
        var authority = await ManagedHumanEvidence.AuthorityAsync(first);
        var firstFlow = new ManagedTokenFlow(first);
        var secondFlow = new ManagedTokenFlow(second);
        await firstFlow.CacheAsync();
        await secondFlow.CacheAsync();
        await RejectCacheSwapAsync(first, firstFlow, second);
        await RejectCacheSwapAsync(second, secondFlow, first);
        await firstFlow.DecideAsync();
        await secondFlow.DecideAsync();
        var firstCode = await firstFlow.CodeAsync();
        var secondCode = await secondFlow.CodeAsync();
        await RejectCodeSwapAsync(first, firstFlow, firstCode, second);
        await RejectCodeSwapAsync(second, secondFlow, secondCode, first);
        await ManagedJourneyEvidence.ExchangeAsync(first, firstFlow, firstCode);
        await ManagedJourneyEvidence.ExchangeAsync(second, secondFlow, secondCode);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(first));
    }

    /// <summary>Changes only the outer client identifier while retaining the rightful protected request and browser, then requires exact all-client persistence equality.</summary>
    private static async Task RejectCacheSwapAsync(ManagedCacheTestContext owner, ManagedTokenFlow flow, ManagedCacheTestContext foreign)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(owner);
        using var response = await owner.SendAsync(new Dictionary<string, string>(flow.Resume, StringComparer.Ordinal) { [ClientId] = foreign.ClientIdentifier });
        await ManagedTokenEvidence.ErrorAsync(response, HttpStatusCode.BadRequest, InvalidToken, local: true);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(owner));
    }

    /// <summary>Changes only Basic client authentication to the foreign client's valid pair; the rightful code, verifier and callback remain exact and unspent.</summary>
    private static async Task RejectCodeSwapAsync(ManagedCacheTestContext owner, ManagedTokenFlow flow, string code, ManagedCacheTestContext foreign)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(owner);
        using var response = await flow.ExchangeAsync(code, secret: foreign.Secret, clientId: foreign.ClientIdentifier);
        await ManagedTokenEvidence.ErrorAsync(response, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(owner));
    }
}
