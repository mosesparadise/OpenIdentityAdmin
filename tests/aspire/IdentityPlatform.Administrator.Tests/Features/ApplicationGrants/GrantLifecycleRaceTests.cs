using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Orders grant admission and re-enable through actual provider lock waits, committed callbacks and independent persisted observations.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantLifecycleRaceTests(BootstrapFixture fixture)
{
    private const string Reason = "maintenance";
    private const int Accepted = 202;
    private const int Conflict = 409;
    private const int Stale = 412;

    /// <summary>The first committed operation determines whether one queued job or an enabled application exists; the peer cannot commit either side effect.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentApiNodes_When_RevocationAndReenableCommitInEitherObservedOrder_Then_OnlyTheWinnerChangesState(string provider, bool grantFirst)
    {
        var grant = new GrantLifecycleRaceProbe(grantFirst);
        var lifecycle = new GrantLifecycleRaceProbe(!grantFirst);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, grant);
        await using var peer = test.Fixture.Peer(provider, lifecycle);
        await test.SeedAsync(test.Id, 1, 1);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        var native = await test.NativeSnapshotAsync();
        var intent = test.Intent();
        var enable = ApplicationMutationCommand.Lifecycle(test.Id, test.Application.Version!, new(Reason, true), true)!;
        var correlation = Guid.NewGuid();
        // Warm the independent root with a rejected original-version precondition before holding a five-second native transaction.
        var warm = ApplicationMutationCommand.Lifecycle(test.Id, ApplicationVersion.Create(test.Id, before.Policy.Revision + 1), new(Reason, true), true)!;
        Assert.Equal(Stale, (await test.Fixture.ExecuteOnAsync(peer, warm, Guid.NewGuid(), test.Token)).Status);
        grant.Armed = lifecycle.Armed = true;
        Func<Task<Outcome>> admit = () => AdmitAsync(test, intent);
        Func<Task<Outcome>> reenable = async () =>
        {
            var result = await test.Fixture.ExecuteOnAsync(peer, enable, correlation, test.Token);
            return new(null, result, result.Status);
        };
        var winner = grantFirst ? grant : lifecycle;
        var loser = grantFirst ? lifecycle : grant;
        var lease = test.Fixture.Scenario.Account.Journey.Lease;
        var observer = new NativeLockObservation(provider, lease.AdministratorConnection);
        var outcomes = await NativeBlockedRace.InvokeAsync(grantFirst ? admit : reenable, winner.Committing.Task,
            grantFirst ? reenable : admit, loser.Lock.Task, winner.Release, observer.WaitAsync);
        grant.RequireOutcome();
        lifecycle.RequireOutcome();
        Assert.Equal(native, await test.NativeSnapshotAsync());
        var after = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        if (grantFirst)
        {
            await RequireGrantAsync(test, before, after, outcomes);
        }
        else
        {
            await RequireEnableAsync(test, before, after, outcomes, correlation);
        }
    }

    /// <summary>Translates only the specified stale loser into an observable outcome; unexpected admission failures still fail the race.</summary>
    private static async Task<Outcome> AdmitAsync(GrantTestContext test, GrantIntent intent)
    {
        try
        {
            return new(await test.AdmitAsync(intent), null, Accepted);
        }
        catch (ApplicationHttpRejected error) when (error.Status == Stale) { return new(null, null, error.Status); }
    }

    /// <summary>Requires one untouched native target, one queued approval/audit and no lifecycle or credential side effects from the denied enable.</summary>
    private static async Task RequireGrantAsync(GrantTestContext test, ApplicationUpdateSnapshot before,
        ApplicationUpdateSnapshot after, Outcome[] outcomes)
    {
        Assert.Equal(Accepted, outcomes[0].Status);
        var job = Assert.IsType<GrantJobView>(outcomes[0].Job);
        var denied = Assert.IsType<ApplicationMutationResult>(outcomes[1].Application);
        Assert.Equal(Conflict, denied.Status);
        Assert.Null(denied.Application);
        Assert.Null(denied.Version);
        Assert.Null(denied.Credential);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Policy), ApplicationUpdateSnapshot.Hash(after.Policy));
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.LifecycleAudits), ApplicationUpdateSnapshot.Hash(after.LifecycleAudits));
        var history = await GrantAuditWitness.ReadAsync(test, job.Id);
        history.Require();
        Assert.Equal(GrantJobState.Queued, history.Job.State);
        Assert.Single(history.Approvals);
        var audit = Assert.Single(history.Audits);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(after.Audits.Where(row => row.Id != audit.Id)));
        Assert.Single(after.Audits, row => row.Id == audit.Id);
    }

    /// <summary>Requires exact successful enable provenance with no job, approval or grant audit left by the stale admission.</summary>
    private static async Task RequireEnableAsync(GrantTestContext test, ApplicationUpdateSnapshot before,
        ApplicationUpdateSnapshot after, Outcome[] outcomes, Guid correlation)
    {
        var enabled = Assert.IsType<ApplicationMutationResult>(outcomes[0].Application);
        ApplicationLifecycleOracle.Require(test.Fixture, before, after, true, correlation);
        ApplicationLifecycleOracle.Output(enabled, after);
        Assert.Equal(Stale, outcomes[1].Status);
        Assert.Null(outcomes[1].Job);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<ApplicationGrantJob>().AnyAsync(test.Token));
        Assert.False(await database.Set<ApplicationGrantApproval>().AnyAsync(test.Token));
        Assert.False(await database.Set<ApplicationGrantAudit>().AnyAsync(test.Token));
    }

    /// <summary>Retains each original coordinator result without disguising application error statuses as admission success.</summary>
    private sealed record Outcome(GrantJobView? Job, ApplicationMutationResult? Application, int Status);
}
