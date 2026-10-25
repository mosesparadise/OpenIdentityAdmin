using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual provider rollback, committed acknowledgement loss and real scope-disposal failures through protected logout.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutFailureTests(BootstrapFixture fixture)
{
    private const string UnknownOutcome = "Local sign-out could not be confirmed.";
    private const string RemoteOutcome = "Issuer sign-out was not requested.";
    private const string DiagnosticMarker = "Synthetic";
    private const string ProviderTrait = "Provider";
    private const string CleanupFailure = "Synthetic local logout cleanup failure.";
    private const int CleanupSeconds = 10;
    private static readonly bool[] Variants = [false, true];

    /// <summary>Denied browser or B1 audit INSERT rolls back every effect and preserves authentication; restoring permission admits the same request.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_AuditInsertDenied_When_AuthenticatedLogoutRuns_Then_OriginalTicketRemainsUsableUntilRestoredPositiveControl(string provider, bool ticketAudit)
    {
        var observation = new BrowserLoginAuditObservation();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, services => services
            .AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(observation)));
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var before = await scenario.SnapshotAsync();
        ExceptionDispatchInfo? primary = null;
        try
        {
            await PermissionAsync(scenario, ticketAudit, false, TestContext.Current.CancellationToken);
            using var response = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
            await RequireUnconfirmedAsync(response);
            Assert.True(scenario.Database.Access.IsDenied(observation.Failure));
            Assert.Equal(before, await scenario.SnapshotAsync());
            using var status = await scenario.Browser.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
            Assert.Contains("\"authenticated\":true", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await PermissionAsync(scenario, ticketAudit, true, cleanup.Token);
        }
        catch (Exception error) when (primary is not null) { throw new AggregateException(primary.SourceException, error); }
        primary?.Throw();
        using var control = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
        await BrowserLogoutHttp.CommittedAsync(control);
        await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: true);
    }

    /// <summary>Real commit acknowledgement or outer scope disposal failure publishes no cookies; a later explicit reconciliation adds no durable effects.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CommittedLogout_When_AcknowledgementOrScopeCleanupFails_Then_FiniteUnconfirmedResponseAllowsBoundedReconciliation(string provider, bool disposal)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var loss = new BrowserCommitLoss();
        var cleanup = new BrowserLoginDisposalProbe(false, () => throw new InvalidOperationException(CleanupFailure)) { Armed = true };
        await using var peer = await scenario.PeerAsync(services =>
        {
            if (disposal)
            {
                cleanup.Configure(services);
            }
            else
            {
                services.AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(loss));
            }
        });
        using var browser = peer.Browser(scenario.Cookies);
        using var response = await BrowserLogoutHttp.PostAsync(browser, token);
        await RequireUnconfirmedAsync(response);
        Assert.Equal(1, disposal ? cleanup.Calls : loss.Commits);
        await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: true);
        var before = await scenario.SnapshotAsync();
        using var reconciled = await BrowserLogoutHttp.PostAsync(scenario.Browser, await BrowserLogoutHttp.EntryAsync(scenario.Browser));
        await BrowserLogoutHttp.CommittedAsync(reconciled);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, peer.Bridge.TokenSends);
    }

    /// <summary>Crossing the incoming proof deadline during actual disposal cannot be hidden by a newly committed five-minute tombstone.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalProofExpiresDuringDisposal_When_LogoutCommits_Then_NoRenewedProofOrSuccessIsPublished(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var first = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(first.ExpiresAt, TimeSpan.Zero).AddSeconds(-1);
        var probe = new BrowserLoginDisposalProbe(false, () => scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(first.ExpiresAt, TimeSpan.Zero)) { Armed = true };
        await using var peer = await scenario.PeerAsync(probe.Configure);
        using var browser = peer.Browser(scenario.Cookies);
        using var response = await BrowserLogoutHttp.PostAsync(browser, token);
        await RequireUnconfirmedAsync(response);
        Assert.Equal(1, probe.Calls);
        await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: false);
        using var expired = await scenario.Browser.GetAsync(BrowserLogoutHttp.Path, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(expired, HttpStatusCode.BadRequest);
    }

    /// <summary>Requires finite sanitized uncertainty rather than a false success, cookie clear or escaping disposed-service error.</summary>
    private static async Task RequireUnconfirmedAsync(HttpResponseMessage response)
    {
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        BrowserLogoutHttp.RequirePageHeaders(response);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(UnknownOutcome, html, StringComparison.Ordinal);
        Assert.Contains(RemoteOutcome, html, StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticMarker, html, StringComparison.Ordinal);
    }

    /// <summary>Toggles only the selected actual runtime audit INSERT privilege, retaining independent cleanup cancellation.</summary>
    private static Task PermissionAsync(BrowserLoginFixture scenario, bool ticket, bool allow, CancellationToken token) => ticket
        ? scenario.Database.Access.SetTicketAuditAsync(allow, token) : scenario.Database.Access.SetBrowserAuditAsync(allow, token);

    /// <summary>Discovers each independent failure boundary against each real provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from variant in Variants
                                                                    select new TheoryDataRow<string, bool>(provider, variant).WithTrait(ProviderTrait, provider);
}
