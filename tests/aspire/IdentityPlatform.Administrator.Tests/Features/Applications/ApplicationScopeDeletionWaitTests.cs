using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires actual independent provider contention when catalogue deletion follows a scope assignment's native commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeDeletionWaitTests(BootstrapFixture fixture)
{
    private const string GuidFormat = "D";
    private const string GovernanceUpdate = "%UPDATE%AdministratorGovernance%";
    private const string Permission = "scp:email";
    private const int Ok = 200;
    private const int WatchdogSeconds = 30;

    /// <summary>A worker observed waiting behind the actual assigning transaction must reread its newly committed reference and preserve the native scope.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedDeletion_When_WorkerWaitsBehindCommittingAssignment_Then_CurrentNativeReferenceRejectsDeletion(string provider)
    {
        var probe = new GrantLifecycleRaceProbe(winner: true);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, probe);
        var selected = (await ApplicationScopeEvidence.SeedAsync(test))[0];
        var created = await test.ExecuteAsync(test.Create());
        var intent = CatalogueDeleteIntent.Create(CatalogueResource.Scope, selected.Id, selected.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
        var admitted = await CatalogueDeletionTest.AdmitAsync(test, intent);
        Assert.Equal(202, admitted.Status);
        await using var peer = test.Peer(provider);
        var command = ApplicationMutationCommand.AssignScopes(created.Application!.Id, created.Version!, new([selected.Name]))!;
        await ContendAsync(test, probe, command, admitted.Job!.Id, peer);
        probe.RequireOutcome();
        await CatalogueDeletionTest.RequireTerminalAsync(test, admitted.Job.Id, CatalogueDeletionFailure.Referenced);
        Assert.Contains(Permission, (await ApplicationScopeEvidence.ReadAsync(test, created.Application.Id)).Permissions);
    }

    /// <summary>Binds the observer to the writer's actual native session, joins both operations and releases the held commit even when observation fails.</summary>
    private static async Task ContendAsync(ApplicationMutationFixture test, GrantLifecycleRaceProbe probe, ApplicationMutationCommand command, Guid job, ServiceProvider peer)
    {
        probe.Armed = true;
        var writing = test.ExecuteAsync(command);
        Task<bool>? worker = null;
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(probe.Committing.Task, writing).WaitAsync(TimeSpan.FromSeconds(WatchdogSeconds), test.Token);
            Assert.True(probe.Committing.Task.IsCompletedSuccessfully);
            var holder = await probe.Committing.Task;
            worker = CatalogueDeletionTest.ProcessAsync(test, job, peer);
            await DatabaseWaiters.ObserveOwnedAsync(test.Scenario.Account.Journey.Lease, holder, GovernanceUpdate, 1, test.Token);
            Assert.False(worker.IsCompleted);
        }
        catch (Exception error) { failures.Add(error); }
        finally { probe.Release.TrySetResult(); }
        var joined = Task.WhenAll(writing, (Task?)worker ?? Task.CompletedTask);
        try
        {
            await joined;
        }
        catch (Exception error)
        {
            if (joined.Exception is { } aggregate)
            {
                failures.AddRange(aggregate.InnerExceptions);
            }
            else
            {
                failures.Add(error);
            }
        }
        if (failures.Count != 0)
        {
            throw new AggregateException(failures);
        }
        Assert.True(await worker!);
        Assert.Equal(Ok, (await writing).Status);
    }
}
