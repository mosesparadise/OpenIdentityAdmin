using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires all-or-none durable state under real audit denial, final expiry and committed-but-unacknowledged completion.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationFailureTests(BootstrapFixture fixture)
{
    private const int LateReservationMinutes = 4;
    private const int IntentMinutes = 5;
    private const int CleanupSeconds = 10;
    private const string MutatedSubject = "mutated-owner-input";
    private const string PostgresPermission = "42501";
    private const int SqlPermission = 229;

    /// <summary>Actual runtime INSERT denial rolls back ticket staging and preserves a former valid session on failed replacement/logout.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuditPermissionDenied_When_BrowserTransitionsWrite_Then_TicketsGenerationsAndIntentsRollBackTogether(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        await DeniedAuditAsync(scenario, async () => { _ = await scenario.StartAsync(browser, intent); });
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        await DeniedAuditAsync(scenario, async () => { _ = await scenario.ReserveAsync(browser, intent, 1, reservation); });
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        await DeniedAuditAsync(scenario, async () => { _ = await scenario.EstablishAsync(browser, intent, 1, reservation, ticket); });
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        await DeniedAuditAsync(scenario, async () => { _ = await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1, ticket.Digest); });
        await DeniedAuditAsync(scenario, async () => { _ = await scenario.LogoutAsync(browser, 1, ticket.Digest, BrowserCoordinationFixture.Digest()); });
        Assert.True(await scenario.CurrentAsync(browser, 1, ticket.Digest));
    }

    /// <summary>Final original intent expiry denies after valid ticket staging even when the prepared ticket has a later deadline.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FinalSaveDelay_When_OriginalIntentExpires_Then_ValidPreparedTicketCannotExtendTheIntent(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var start = scenario.Clock.GetUtcNow();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        scenario.Clock.Set(start.AddMinutes(LateReservationMinutes));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        var before = await scenario.SnapshotAsync();
        var fault = new BrowserSaveObservation(() => scenario.Clock.Set(start.AddMinutes(IntentMinutes)));
        Assert.False(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket, fault));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>One actual commit may survive a lost acknowledgement; the intent stays spent and no second ticket can be established.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommitAcknowledgementLoss_When_EstablishmentCompletes_Then_DurableSpentStateDeniesRepetition(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        var loss = new BrowserCommitLoss();
        Assert.Same(loss.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.EstablishAsync(browser, intent, 1, reservation, ticket, loss)));
        Assert.Equal(1, loss.Commits);
        Assert.True(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        var before = await scenario.SnapshotAsync();
        Assert.False(await scenario.EstablishAsync(browser, intent, 1, reservation, scenario.Ticket(), loss));
        Assert.True(before == await scenario.SnapshotAsync());
        Assert.Equal(1, loss.Commits);
    }

    /// <summary>Caller-owned metadata and payload can change during a wait without altering the private authenticated persistence snapshot.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CallerMutationDuringSave_When_TicketIsEstablished_Then_OnlyTheAuthenticatedSnapshotIsCommitted(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        var subject = ticket.Subject;
        var payload = ticket.ProtectedTicket.ToArray();
        var fault = new BrowserSaveObservation(() => { ticket.Subject = MutatedSubject; ticket.ProtectedTicket[0] ^= 1; });
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket, fault));
        Assert.True(fault.Calls > 0 && ticket.Subject == MutatedSubject && !ticket.ProtectedTicket.SequenceEqual(payload));
        await scenario.RunAsync(async (database, codec) =>
        {
            var stored = await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token);
            Assert.True(codec.IsAuthentic(stored, scenario.Clock.GetUtcNow()) && stored.Subject == subject);
            Assert.True((await database.Set<BrowserSessionAudit>().SingleAsync(row => row.Operation == BrowserMutation.Establish, BrowserCoordinationFixture.Token)).Subject == subject);
            return true;
        });
    }

    /// <summary>Cancellation after actual ticket staging rolls back new coordination and both audit categories rather than leaving a partial established intent.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CancellationAfterNativeSave_When_EstablishmentIsPending_Then_AllNewOwnedEffectsRollBack(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var before = await scenario.SnapshotAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(BrowserCoordinationFixture.Token);
        var observation = new BrowserSaveObservation(cancellation.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scenario.RunAsync((database, codec) =>
            new BrowserLoginCompletion(database, scenario.Clock, codec).EstablishAsync(browser, intent, 1, reservation,
                scenario.Ticket(), cancellation.Token), observation));
        Assert.True(observation.Calls > 0);
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Restores permission under independent cancellation and compares all rows after the exact provider denial.</summary>
    private static async Task DeniedAuditAsync(BrowserCoordinationFixture scenario, Func<Task> action)
    {
        var before = await scenario.SnapshotAsync();
        ExceptionDispatchInfo? primary = null;
        try
        {
            await scenario.Lease.Access.SetBrowserAuditAsync(false, BrowserCoordinationFixture.Token);
            var error = await Record.ExceptionAsync(action);
            var update = Assert.IsType<DbUpdateException>(error);
            Assert.NotNull(update.InnerException);
            Assert.True(update.InnerException is Npgsql.PostgresException { SqlState: PostgresPermission } or Microsoft.Data.SqlClient.SqlException { Number: SqlPermission });
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await scenario.Lease.Access.SetBrowserAuditAsync(true, cleanup.Token);
        }
        catch (Exception error) when (primary is not null) { throw new AggregateException(primary.SourceException, error); }
        primary?.Throw();
        Assert.True(before == await scenario.SnapshotAsync());
    }
}
