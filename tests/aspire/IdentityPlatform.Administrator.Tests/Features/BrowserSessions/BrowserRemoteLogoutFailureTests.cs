using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Targets the actual remote phase after local commit, distinguishing rollback from durable but unacknowledged completion.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutFailureTests(BootstrapFixture fixture)
{
    private const int LifetimeMinutes = 5;

    private const string SaveFault = "save";
    private const string AcknowledgementFault = "acknowledgement";
    private const string CleanupFault = "cleanup";
    private const string SaveExpiryFault = "save-expiry";
    private const string CleanupExpiryFault = "cleanup-expiry";
    private const string ProviderTrait = "Provider";
    private static readonly bool[] Variants = [false, true];
    private const string Failure = "Synthetic remote cleanup failure.";
    private static readonly string[] Faults = [SaveFault, AcknowledgementFault, CleanupFault, SaveExpiryFault, CleanupExpiryFault];

    /// <summary>Remote faults cannot reclassify known local revocation; return faults cannot publish correlation consumption or authentication.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_KnownLocalLogout_When_RemotePhaseFails_Then_LocalRemainsRevokedAndRemoteSuccessIsWithheld(string provider, bool returned, string failure)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        if (returned)
        {
            await scenario.BeginAsync();
        }
        var fault = new BrowserRemoteLogoutFault(returned ? BrowserMutation.Return : BrowserMutation.RemotePending)
        {
            LoseAcknowledgement = failure == AcknowledgementFault
        };
        var original = scenario.Login.Issuer.Account.Journey.Clock.GetUtcNow();
        Action expire = () => scenario.Login.Issuer.Account.Journey.Clock.Now = original.AddMinutes(LifetimeMinutes);
        fault.AfterSave = failure == SaveFault ? () => throw new InvalidOperationException(Failure) : failure == SaveExpiryFault ? expire : null;
        fault.AfterCleanup = failure == CleanupFault ? () => throw new InvalidOperationException(Failure) : failure == CleanupExpiryFault ? expire : null;
        await using var peer = await scenario.Login.PeerAsync(fault.Configure);
        using var browser = peer.Browser(scenario.Login.Cookies);
        using var response = returned ? await browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken) : await scenario.PostAsync(browser);
        if (returned)
        {
            BrowserLoginTests.RequireDenied(response, failure == SaveExpiryFault ? HttpStatusCode.Conflict : HttpStatusCode.ServiceUnavailable);
        }
        else
        {
            await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
            Assert.Null(scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]);
        }
        Assert.Equal(1, fault.Saves);
        if (failure is CleanupFault or CleanupExpiryFault)
        {
            Assert.Equal(1, fault.Cleanups);
        }
        var committed = failure is AcknowledgementFault or CleanupFault or CleanupExpiryFault;
        Assert.Equal(committed ? 1 : 0, fault.Commits);
        var expected = returned ? committed ? BrowserLogoutState.ReturnedUnverified : BrowserLogoutState.RemotePending
            : committed ? BrowserLogoutState.RemotePending : BrowserLogoutState.LocalCommitted;
        await scenario.RequireContinuationAsync(expected);
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
        Assert.Equal(0, peer.Bridge.ParSends);
        Assert.Equal(0, peer.Bridge.TokenSends);
    }

    /// <summary>Each fault selects a real persistence phase and provider; no unrelated first commit can count as the required witness.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, string>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                            from returned in Variants
                                                                            from fault in Faults
                                                                            select new TheoryDataRow<string, bool, string>(provider, returned, fault).WithTrait(ProviderTrait, provider);
}
