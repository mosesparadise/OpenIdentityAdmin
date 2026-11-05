using System.Runtime.ExceptionServices;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires database-observed contention between independent API roots before testing retained-key and incomplete-job uniqueness.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantAdmissionRaceTests(BootstrapFixture fixture)
{
    private const int Contenders = 2;
    private const int Conflict = 409;
    private const int Accepted = 202;

    /// <summary>Concurrent identical intents converge on one approval; different keys conflict without creating a second incomplete job or audit.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentApiNodes_When_GrantAdmissionsActuallyContend_Then_OneJobAndApprovalCommit(string provider, bool sameKey)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await using var peer = test.Fixture.Peer(provider);
        var intent = test.Intent();
        var lease = test.Fixture.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var first = OutcomeAsync(() => test.AdmitAsync(intent));
        var second = OutcomeAsync(() => test.AdmitOnAsync(peer, sameKey ? intent : test.Intent()));
        var outcomes = await JoinAsync(held, () => held.ObserveOwnedAsync(lease, Contenders, test.Token), first, second);
        var jobs = outcomes.Where(value => value.Job is not null).Select(value => value.Job!).ToArray();
        Assert.Equal(sameKey ? 2 : 1, jobs.Length);
        Assert.All(jobs, job => Assert.Equal(jobs[0].Id, job.Id));
        Assert.Equal(sameKey ? 0 : 1, outcomes.Count(value => value.Status == Conflict));
        var history = await GrantAuditWitness.ReadAsync(test, jobs[0].Id);
        history.Require();
        Assert.Single(history.Approvals);
        Assert.Single(history.Audits);
        Assert.Equal(1, history.Job.Revision);
        Assert.Equal(GrantJobState.Queued, history.Job.State);
    }

    /// <summary>Retains only the documented conflict; unexpected exceptions still fail the joined race.</summary>
    private static async Task<(GrantJobView? Job, int Status)> OutcomeAsync(Func<Task<GrantJobView>> admit)
    {
        try
        {
            return (await admit(), Accepted);
        }
        catch (ApplicationHttpRejected error) when (error.Status == Conflict) { return (null, Conflict); }
    }

    /// <summary>Always releases the native blocker and joins both operations before rethrowing the primary observation failure.</summary>
    private static async Task<(GrantJobView? Job, int Status)[]> JoinAsync(GovernanceLock held, Func<Task> observe,
        params Task<(GrantJobView? Job, int Status)>[] requests)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            await observe();
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await held.DisposeAsync(); }
        var result = await Task.WhenAll(requests);
        failure?.Throw();
        return result;
    }
}
