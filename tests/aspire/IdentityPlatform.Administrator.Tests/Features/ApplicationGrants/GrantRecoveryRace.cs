using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Runs a genuine failed worker through cleanup and observed failure-recording contention without manufacturing its retained attempt.</summary>
internal static class GrantRecoveryRace
{
    internal const string Progress = "progress";
    internal const string Approval = "approval";
    internal const string Success = "success";
    private const string GuidFormat = "D";
    private const string Reason = "security";

    /// <summary>Always releases and joins original cleanup, even if preparation, observation or the competing command fails.</summary>
    internal static async Task<GrantAuditWitness> RunAsync(GrantTestContext test, string provider, string id, string outcome,
        GrantFailureCapture fault, GrantAuthorityRaceProbe winner, ServiceProvider peer)
    {
        fault.Armed = true;
        var pending = test.BatchAsync(id);
        GrantAuditWitness? before = null;
        Exception? error = null;
        try
        {
            await Task.WhenAny(fault.Entered.Task, pending).WaitAsync(GrantFailureCapture.Watchdog, test.Token);
            Assert.True(fault.Entered.Task.IsCompletedSuccessfully);
            Assert.False(pending.IsCompleted);
            var intent = await PrepareAsync(test, provider, peer, id, outcome);
            before = await GrantAuditWitness.ReadAsync(test, id);
            winner.Armed = true;
            await OrderAsync(test, provider, id, intent, peer, pending, fault, winner, outcome == Progress);
        }
        catch (Exception failure) { error = failure; }
        finally { fault.Release.TrySetResult(); }
        try
        {
            await pending;
        }
        catch (Exception failure) { error = error is null ? failure : new AggregateException(error, failure); }
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
        fault.Require(outcome == Progress);
        winner.Require(true, false);
        return before!;
    }

    /// <summary>Commits only real prerequisite transitions: one native batch before success, or a separate actual failed batch before fresh continuation.</summary>
    private static async Task<GrantIntent?> PrepareAsync(GrantTestContext test, string provider, ServiceProvider peer, string id, string outcome)
    {
        if (outcome == Success)
        {
            Assert.True(await BatchAsync(test, peer, id));
        }
        if (outcome != Approval)
        {
            return null;
        }
        var stop = new GrantCommitFault(after: false) { Armed = true };
        await using var failing = test.Fixture.Peer(provider, stop);
        Assert.True(await BatchAsync(test, failing, id));
        Assert.True(stop.Observed);
        var stopped = await test.ReadAsync(id);
        Assert.Equal(GrantJobState.Stopped, stopped.State);
        return GrantIntent.Create(test.Id, test.Application.Version!, Guid.NewGuid().ToString(GuidFormat),
            new(Reason, true, ApplicationVersion.Create(id, stopped.Revision)), stopped.Id)!;
    }

    /// <summary>Releases original cleanup only after the peer reaches commit, then proves the fresh failure transaction waits on that exact native session.</summary>
    private static async Task OrderAsync(GrantTestContext test, string provider, string id, GrantIntent? intent, ServiceProvider peer,
        Task<bool> pending, GrantFailureCapture fault, GrantAuthorityRaceProbe winner, bool stopped)
    {
        // Starts the actual peer operation once after all prerequisite state has committed.
        Task<bool> AdvanceAsync() => AdvanceAsyncCore(test, peer, id, intent);
        // Opens only original cleanup; production captures and records its own failure after draining.
        Task<bool> ReleaseAsync()
        {
            fault.Release.TrySetResult();
            return pending;
        }
        var lease = test.Fixture.Scenario.Account.Journey.Lease;
        var observer = new NativeLockObservation(provider, lease.AdministratorConnection);
        var results = await NativeBlockedRace.InvokeAsync(AdvanceAsync, winner.Committing.Task,
            ReleaseAsync, fault.RecoveryLock.Task, winner.Release, observer.WaitAsync);
        Assert.True(results[0]);
        Assert.Equal(stopped, results[1]);
    }

    /// <summary>Dispatches the prepared real continuation or batch without directly writing a job, approval or failure record.</summary>
    private static async Task<bool> AdvanceAsyncCore(GrantTestContext test, ServiceProvider peer, string id, GrantIntent? intent)
    {
        if (intent is null)
        {
            return await BatchAsync(test, peer, id);
        }
        Assert.Equal(id, (await test.AdmitOnAsync(peer, intent)).Id);
        return true;
    }

    /// <summary>Runs a fresh owned worker request on the selected independent node with the unchanged product clock and caller token.</summary>
    private static Task<bool> BatchAsync(GrantTestContext test, ServiceProvider peer, string id) =>
        GrantBatchOperation.ExecuteAsync(peer.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(id), test.Fixture.Clock, test.Token);
}
