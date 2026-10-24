using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Distinguishes committed local orphans from successful browser delivery after actual native callback execution.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackFailureTests(BootstrapFixture fixture)
{
    private const string Commit = "commit";
    private const string LocalDisposal = "local-disposal";
    private const string NativeDisposal = "native-disposal";
    private const string SourceExpiry = "source-expiry";
    private const string Expiry = "expiry";
    private const string ProviderTrait = "Provider";

    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] Faults = [Commit, LocalDisposal, NativeDisposal, Expiry, SourceExpiry];

    /// <summary>Lost acknowledgements, failed real cleanup and original expiry publish no success despite a committed ticket; replay cannot recover it.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_CommittedProductCallback_When_FinalCompletionFails_Then_OrphanTicketPublishesNoCookieAndCannotReplay(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        var pending = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        var probe = new BrowserCallbackCompletionProbe(fault, sourceDeadline => scenario.Issuer.Account.Journey.Clock.Now =
            fault == SourceExpiry ? sourceDeadline ?? throw new InvalidOperationException() : new DateTimeOffset(pending.ExpiresAt, TimeSpan.Zero));
        await using var peer = await scenario.PeerAsync(probe.Configure);
        if (fault == SourceExpiry)
        {
            peer.Bridge.IdentityFault = NativeIdentityResponse.Fault.BrowserExpiry;
        }
        using var response = await BrowserCallbackTests.ReplayAsync(peer, callback, cookies);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(1, peer.Bridge.TokenSends);
        if (fault == SourceExpiry)
        {
            Assert.Equal(1, peer.Bridge.IdentityRewrites);
            var deadline = Assert.IsType<DateTimeOffset>(probe.SourceDeadline);
            Assert.True(deadline.UtcTicks < pending.ExpiresAt);
            Assert.Equal(deadline, scenario.Issuer.Account.Journey.Clock.Now);
            var row = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
            Assert.Equal(deadline.UtcTicks, row.AbsoluteExpiresAt);
        }
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Establish, TestContext.Current.CancellationToken)));
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Discovers actual establishment acknowledgement and scope-disposal failure boundaries on both stores.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Variants => from provider in ProviderNames
                                                                         from fault in Faults
                                                                         select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
