using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Retains original reconciliation bounds and suppresses every response cookie after actual read-scope cleanup failure.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutRecoveryFailureTests(BootstrapFixture fixture)
{
    private const string CleanupFailure = "Synthetic reconciliation cleanup failure.";
    private const string ProviderTrait = "Provider";
    private static readonly bool[] Failures = [false, true];

    /// <summary>Recovery's rolled-back writer acquisition remains read-only even when scope disposal fails or the original tombstone expires.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ValidReconciliation_When_ScopeDisposalFailsOrCrossesOriginalExpiry_Then_NoCookiesOrSuccessEscapeAndStateRemainsUnchanged(string provider, bool expiry)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var delivered = await BrowserLogoutReconciliationTests.LostAsync(scenario);
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var deadline = await scenario.ReadAsync(db => db.Set<BrowserContext>().Select(row => row.ExpiresAt).SingleAsync(TestContext.Current.CancellationToken));
        var probe = new BrowserStatusReadProbe(false, () =>
        {
            if (expiry)
            {
                scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(deadline, TimeSpan.Zero);
            }
            else
            {
                throw new InvalidOperationException(CleanupFailure);
            }
        })
        {
            Armed = true
        };
        await using var peer = await scenario.PeerAsync(probe.Configure);
        using var browser = peer.Browser(scenario.Cookies);
        var before = await scenario.SnapshotAsync();
        using var response = await BrowserLogoutHttp.PostAsync(browser, token);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Contains("Local sign-out could not be confirmed.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, peer.Bridge.TokenSends);
    }

    /// <summary>Discovers both post-disposal failure modes on each real database.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from expiry in Failures
                                                                    select new TheoryDataRow<string, bool>(provider, expiry).WithTrait(ProviderTrait, provider);
}
