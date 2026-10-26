using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires local success without remote authority under quota pressure, anonymous cancellation and acknowledgement recovery.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutCapacityTests(BootstrapFixture fixture)
{
    private const int RetainedLimit = 10000;
    private const int LiveLimit = 1024;

    private const string ChoiceField = "Mode";
    private const string IssuerChoice = "issuer";
    private const string LocalCleanupFailure = "Synthetic local cleanup failure.";
    private const string ProviderTrait = "Provider";
    private static readonly bool[] Variants = [false, true];

    /// <summary>Either exact retained or live continuation ceiling withholds navigation without evicting rows or denying local revocation.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_RemoteQuotaFull_When_AssistedLogoutRuns_Then_LocalCommitSurvivesAndEveryExistingCorrelationIsPreserved(string provider, bool retained)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.Login.ReadAsync(async db =>
        {
            var browser = BrowserCoordinationSeeds.Context(scenario.Login.Issuer.Account.Journey.Clock.GetUtcNow().UtcTicks);
            db.Add(browser);
            for (var index = 0; index < (retained ? RetainedLimit : LiveLimit); index++)
            {
                db.Add(BrowserCoordinationSeeds.Logout(browser, !retained));
            }
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var before = await RowsAsync(scenario.Login);
        using var response = await scenario.PostAsync();
        await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
        Assert.Equal(before, await RowsAsync(scenario.Login));
        Assert.Null(scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Session]);
        Assert.Null(scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]);
        Assert.Equal(1, await scenario.Login.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken)));
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
    }

    /// <summary>An anonymous pending cancellation can request the choice but cannot manufacture authenticated remote provenance.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AnonymousPendingIntent_When_AssistedChoiceIsPosted_Then_OnlyLocalCancellationCommits(string provider)
    {
        await using var login = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await login.StartAsync(await login.EntryAsync());
        var token = await BrowserLogoutHttp.EntryAsync(login.Browser);
        using var response = await BrowserIssuerHttp.PostAsync(login.Browser, BrowserLogoutHttp.Path,
            new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = token, [ChoiceField] = IssuerChoice });
        await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
        await BrowserLogoutTests.RequireCommittedAsync(login, authenticated: false);
    }

    /// <summary>A local commit whose acknowledgement is lost cannot start remote work; explicit reconciliation never reconstructs its reservation.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_AssistedLocalCommit_When_LocalAcknowledgementOrCleanupFails_Then_ReconciliationIsStrictlyLocal(string provider, bool cleanup)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        var fault = new BrowserRemoteLogoutFault(BrowserMutation.Logout)
        {
            LoseAcknowledgement = !cleanup,
            AfterCleanup = cleanup ? () => throw new InvalidOperationException(LocalCleanupFailure) : null
        };
        await using var peer = await scenario.Login.PeerAsync(fault.Configure);
        using var browser = peer.Browser(scenario.Login.Cookies);
        using var response = await scenario.PostAsync(browser);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, fault.Commits);
        if (cleanup)
        {
            Assert.Equal(1, fault.Cleanups);
        }
        await scenario.RequireContinuationAsync(BrowserLogoutState.LocalCommitted);
        var before = await scenario.Login.SnapshotAsync();
        using var reconciled = await scenario.PostAsync();
        await BrowserRemoteLogoutFixture.RequireLocalAsync(reconciled, link: false);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
        Assert.Null(scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]);
    }

    /// <summary>Hashes complete stored correlation rows without exposing private selector provenance in failed assertions.</summary>
    private static Task<string> RowsAsync(BrowserLoginFixture login) => login.ReadAsync(async db => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(await db.Set<BrowserLogoutContinuation>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(TestContext.Current.CancellationToken)))));

    /// <summary>Preserves native provider discovery for both independent variants.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from variant in Variants
                                                                    select new TheoryDataRow<string, bool>(provider, variant).WithTrait(ProviderTrait, provider);
}
