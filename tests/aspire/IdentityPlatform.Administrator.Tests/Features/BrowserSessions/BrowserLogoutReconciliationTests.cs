using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Rejects every nearby state that is not the single immediately preceding committed logout.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutReconciliationTests(BootstrapFixture fixture)
{
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    private const string ProviderTrait = "Provider";
    private const string Generation = "generation";
    private const string Ticket = "ticket";
    private const string Pending = "pending";
    private const string Reserved = "reserved";
    private const string MissingAudit = "missing-audit";
    private const string DuplicateAudit = "duplicate-audit";
    private const string Operation = "operation";
    private const string Correlation = "correlation";
    private const string Expired = "expired";
    private static readonly string[] Faults = [Generation, Ticket, Pending, Reserved, MissingAudit, DuplicateAudit, Operation, Correlation, Expired];

    /// <summary>Each fault changes one durable predicate after a genuine protected logout; the original authentic browser proof cannot repair or adopt it.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_LostLogoutResponse_When_CurrentTombstoneCannotProveItsExactOrigin_Then_OriginalProofDeniesWithoutAnyDurableChange(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var delivered = await LostAsync(scenario);
        await AlterAsync(scenario, fault);
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.Browser.GetAsync(BrowserLogoutHttp.Path, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>A newer genuine explicit login between recovery GET and POST is retained; the old confirmation never cancels that generation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidReconciliationForm_When_NewLoginReplacesTheTombstone_Then_StaleConfirmationCannotMutateNewIntent(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var delivered = await LostAsync(scenario);
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        _ = await scenario.StartAsync(await scenario.EntryAsync(delivered), delivered);
        var before = await scenario.SnapshotAsync();
        using var response = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Runs actual protected logout with a detached cookie jar, leaving the original browser exactly as if the response were not delivered.</summary>
    internal static async Task<HttpClient> LostAsync(BrowserLoginFixture scenario)
    {
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var cookies = new CookieContainer();
        cookies.SetCookies(scenario.Profile.ClientUri, scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri).Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        var delivered = scenario.Host.Browser(cookies);
        try
        {
            using var response = await BrowserLogoutHttp.PostAsync(delivered, token);
            await BrowserLogoutHttp.CommittedAsync(response);
            return delivered;
        }
        catch { delivered.Dispose(); throw; }
    }

    /// <summary>Uses isolated test writes solely to corrupt one independently asserted predicate, never to establish positive product authentication.</summary>
    private static Task<bool> AlterAsync(BrowserLoginFixture scenario, string fault) => scenario.ReadAsync(async database =>
    {
        var context = await database.Set<BrowserContext>().SingleAsync(TestContext.Current.CancellationToken);
        var audit = await database.Set<BrowserSessionAudit>().SingleAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken);
        if (fault == Generation)
        {
            context.Generation++;
        }
        if (fault == Ticket)
        {
            context.TicketDigest = BrowserCoordinationFixture.Digest();
        }
        if (fault is Pending or Reserved)
        {
            var intent = await database.Set<BrowserLoginIntent>().SingleAsync(TestContext.Current.CancellationToken);
            intent.State = fault == Pending ? BrowserIntentState.Pending : BrowserIntentState.Reserved;
        }
        if (fault == MissingAudit)
        {
            database.Remove(audit);
        }
        if (fault == DuplicateAudit)
        {
            database.Add(new BrowserSessionAudit
            {
                Id = Guid.NewGuid(),
                CorrelationId = audit.CorrelationId,
                Generation = audit.Generation,
                Operation = audit.Operation,
                OccurredAt = audit.OccurredAt
            });
        }
        if (fault == Operation)
        {
            audit.Operation = BrowserMutation.Start;
        }
        if (fault == Correlation)
        {
            audit.CorrelationId = Guid.NewGuid();
        }
        if (fault == Expired)
        {
            scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(context.ExpiresAt, TimeSpan.Zero);
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return true;
    });

    /// <summary>Enumerates exact successor, association, pending-work, provenance and expiry controls on both stores.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                      from fault in Faults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
