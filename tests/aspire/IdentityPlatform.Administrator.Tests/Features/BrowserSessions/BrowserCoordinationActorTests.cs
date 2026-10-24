using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Rejects purported actor provenance that does not authenticate the current owned ticket and its live server lifetime.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationActorTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int IdleMinutes = 5;
    private const int BeforeExpiryMinutes = 4;
    private const int NextGeneration = 2;

    /// <summary>Supplies each independent stored-ticket defect on both native providers.</summary>
    public static IEnumerable<TheoryDataRow<string, BrowserActorFault>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Enum.GetValues<BrowserActorFault>()
        select new TheoryDataRow<string, BrowserActorFault>(provider, fault).WithTrait(ProviderTrait, provider);

    /// <summary>A corrupt, revoked, expired or different ticket never attributes its subject to replacement/logout; anonymous invalidation remains possible.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_UntrustedCurrentTicket_When_ActorAttributionIsRequested_Then_DenialPreservesStateAndAnonymousReplacementClaimsNoActor(string provider, BrowserActorFault fault)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        await AlterAsync(scenario, fault);
        var actor = fault == BrowserActorFault.OtherDigest ? BrowserCoordinationFixture.Digest() : ticket.Digest;
        var before = await scenario.SnapshotAsync();
        Assert.Null(await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1, actor));
        Assert.Null(await scenario.LogoutAsync(browser, 1, actor, BrowserCoordinationFixture.Digest()));
        Assert.True(before == await scenario.SnapshotAsync());
        Assert.Equal(2, await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1));
        await scenario.RunAsync(async (database, _) =>
        {
            var audit = await database.Set<BrowserSessionAudit>().SingleAsync(row => row.Generation == NextGeneration, BrowserCoordinationFixture.Token);
            Assert.Null(audit.Subject);
            Assert.Null(audit.SourceSession);
            Assert.NotEqual(Guid.Empty, audit.CorrelationId);
            return true;
        });
    }

    /// <summary>Initially valid actor authority must remain live after real revocation writes; a later operation deadline cannot replace its immutable bound.</summary>
    [Theory, MemberData(nameof(IdentityPlatform.Administrator.Tests.Features.Factors.StoreTests.Providers), MemberType = typeof(IdentityPlatform.Administrator.Tests.Features.Factors.StoreTests))]
    public async Task Given_ActorExpiryDuringSave_When_ReplacementOrLogoutStages_Then_FinalActorGuardRollsBackEveryEffect(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var start = scenario.Clock.GetUtcNow();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        foreach (var logout in new[] { false, true })
        {
            scenario.Clock.Set(start.AddMinutes(BeforeExpiryMinutes));
            var before = await scenario.SnapshotAsync();
            var observation = new BrowserSaveObservation(() => scenario.Clock.Set(start.AddMinutes(IdleMinutes)));
            var result = logout
                ? await scenario.LogoutAsync(browser, 1, ticket.Digest, BrowserCoordinationFixture.Digest(), observation)
                : await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1, ticket.Digest, observation);
            Assert.True(observation.Calls > 0);
            Assert.Null(result);
            Assert.True(before == await scenario.SnapshotAsync());
        }
    }

    /// <summary>Changes one independent persisted boundary without replacing the real codec or its validation result.</summary>
    private static async Task AlterAsync(BrowserCoordinationFixture scenario, BrowserActorFault fault)
    {
        if (fault == BrowserActorFault.Expired)
        {
            scenario.Clock.Set(scenario.Clock.GetUtcNow().AddMinutes(IdleMinutes));
        }
        else if (fault is BrowserActorFault.Corrupt or BrowserActorFault.Revoked)
        {
            await scenario.RunAsync(async (database, _) =>
            {
                var row = await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token);
                if (fault == BrowserActorFault.Corrupt)
                {
                    row.ProtectedTicket[0] ^= 1;
                }
                else
                {
                    row.RevokedAt = scenario.Clock.GetUtcNow().UtcTicks;
                }
                return await database.SaveChangesAsync(BrowserCoordinationFixture.Token);
            });
        }
    }
}

/// <summary>Names independent current-ticket provenance failures.</summary>
public enum BrowserActorFault
{
    OtherDigest, Expired, Corrupt, Revoked
}
