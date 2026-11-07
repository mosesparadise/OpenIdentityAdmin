using System.Diagnostics;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises actual HTTP cancellation, native acquisition timeout and absent migration state without repair or late generation.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotWaitFailureTests(BootstrapFixture fixture)
{
    private const int CancellationSeconds = 3;
    private const int TerminationSeconds = 8;
    private const int OriginalTransactionSeconds = 5;

    /// <summary>The holder stays locked until native command termination; explicit cancellation and original acquisition timeout cannot issue credentials.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PilotExchangeActuallyBlocked_When_CallerCancelsOrOriginalAcquisitionTimesOut_Then_NoLateConsumptionOrGenerationOccurs(string provider, bool standalone, bool cancel)
    {
        var probe = new PilotWaitProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var state = await PilotWaitState.ReadAsync(environment, pilot.Client, flow);
        var before = await PilotWaitState.SnapshotAsync(environment);
        await TerminateWhileWaitingAsync(environment, provider, pilot.Client, flow, probe, cancel);
        Assert.Equal(before, await PilotWaitState.SnapshotAsync(environment));
        await PilotRevokedAuthorization.RequireUnconsumedAsync(environment, state.CodeId);
        await PilotWaitState.PositiveAsync(environment, pilot.Client);
    }

    /// <summary>Joins native command failure while the actual holder remains locked, then releases and drains the original request without retry.</summary>
    private static async Task TerminateWhileWaitingAsync(BrowserHttpsFixture environment, string provider, TestRelyingParty client,
        TestFlow flow, PilotWaitProbe probe, bool cancel)
    {
        await using var gate = await PilotWaitGate.AcquireAsync(environment);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        probe.Armed = true;
        var started = Stopwatch.GetTimestamp();
        probe.StartedAt = started;
        probe.CallerRequested = () => cancellation.IsCancellationRequested;
        var pending = client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: cancellation.Token);
        ExceptionDispatchInfo? failure = null;
        var canceled = false;
        try
        {
            await gate.ObserveAsync(environment, provider, probe, pending);
            Assert.False(probe.Failed.Task.IsCompleted);
            await RequireTerminationAsync(pending, probe, cancellation, cancel, started);
            canceled = cancel;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await gate.CompleteAsync(pending, failure, canceled); }
    }

    /// <summary>Requires server-side command termination before releasing the fixture lock; HTTP cancellation alone cannot satisfy this oracle.</summary>
    private static async Task RequireTerminationAsync(Task<HttpResponseMessage> pending, PilotWaitProbe probe,
        CancellationTokenSource cancellation, bool cancel, long started)
    {
        if (cancel)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await probe.Failed.Task.WaitAsync(TimeSpan.FromSeconds(CancellationSeconds), TestContext.Current.CancellationToken);
            Assert.True(probe.CallerCancellation);
            Assert.False(probe.DeadlineTermination);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(OriginalTransactionSeconds));
        }
        else
        {
            using var response = await pending.WaitAsync(TimeSpan.FromSeconds(TerminationSeconds), TestContext.Current.CancellationToken);
            await PilotWaitState.UnavailableAsync(response);
            Assert.False(cancellation.IsCancellationRequested);
            Assert.True(probe.Failed.Task.IsCompletedSuccessfully);
            Assert.True(probe.DeadlineTermination);
            Assert.False(probe.CallerCancellation);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(TerminationSeconds));
        }
    }

    /// <summary>A genuine otherwise valid code fails without recreating a missing gate; only explicit fixture restoration permits a new native journey.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_MigrationOwnedGateMissing_When_GenuinePilotCodeExchanges_Then_HttpDoesNotRepairOrIssue(string provider, bool standalone)
    {
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var state = await PilotWaitState.ReadAsync(environment, pilot.Client, flow);
        await DeleteGateAsync(environment);
        var before = await PilotWaitState.SnapshotAsync(environment);
        using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await PilotWaitState.UnavailableAsync(response);
        Assert.Equal(before, await PilotWaitState.SnapshotAsync(environment));
        await PilotRevokedAuthorization.RequireUnconsumedAsync(environment, state.CodeId);
        await RestoreGateAsync(environment);
        await PilotWaitState.PositiveAsync(environment, pilot.Client);
    }

    /// <summary>Removes only the isolated fixture singleton after genuine code creation, preserving native protocol rows.</summary>
    private static async Task DeleteGateAsync(BrowserHttpsFixture environment)
    {
        await using var scope = PilotWaitState.Observer(environment);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<PilotProtocolGate>()
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Proves HTTP left the gate absent before an explicit test-only repair; no production request performs this insertion.</summary>
    private static async Task RestoreGateAsync(BrowserHttpsFixture environment)
    {
        await using var scope = PilotWaitState.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<PilotProtocolGate>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        database.Add(new PilotProtocolGate());
        Assert.Equal(1, await database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
