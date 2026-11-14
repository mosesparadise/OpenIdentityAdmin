using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Observes real database waits and both source-edit orderings without assuming that cloning consumes its source version.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneRaceTests(BootstrapFixture fixture)
{
    private const string CloneEvent = "ApplicationCloned";
    private const string Edited = "Edited after source observation";
    private const string Callback = "https://edited.example.test/callback";
    private const string GovernanceUpdate = "%UPDATE%AdministratorGovernance%";
    private const int Created = 201;
    private const int Conflict = 409;
    private const int Contenders = 2;
    private const int WatchdogSeconds = 30;
    private static readonly int[] DuplicateStatuses = [201, 409];

    /// <summary>Two independent API roots genuinely wait on governance; duplicate destination identity permits exactly one fresh credential and audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentCloners_When_SameDestinationActuallyContends_Then_OneCreationAndOneConflictCommit(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var peer = test.Peer(provider);
        var source = await test.ExecuteAsync(test.Create());
        var command = ApplicationCloneAtomicityTests.Clone(test, source.Application!.Id, source.Version!);
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var requests = new[] { test.ExecuteAsync(command), test.ExecuteOnAsync(peer, command, Guid.NewGuid(), test.Token) };
        var results = await ApplicationUpdateTests.ReleaseAndJoinAsync(held, () => held.ObserveWaitersAsync(lease, Contenders), requests);
        Assert.Equal(DuplicateStatuses, results.Select(row => row.Status).Order());
        Assert.NotNull(Assert.Single(results, row => row.Status == Created).Credential);
        Assert.Null(Assert.Single(results, row => row.Status == Conflict).Credential);
        await using var scope = test.Scope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationCloneAuditDetail>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.Equal(2, await test.AuditsAsync());
    }

    /// <summary>Edit-first invalidates the exact source precondition; clone-first preserves an earlier immutable copy while allowing the later source edit.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_SourceAndClone_When_EachCommitOrderingIsObserved_Then_CopyUsesOnlyItsLockedSourceVersion(string provider, bool cloneFirst)
    {
        var probe = new GrantLifecycleRaceProbe(winner: true);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, probe);
        await using var peer = test.Peer(provider);
        var source = await test.ExecuteAsync(test.Create());
        var clone = ApplicationCloneAtomicityTests.Clone(test, source.Application!.Id, source.Version!);
        var edit = ApplicationMutationCommand.Edit(source.Application.Id, source.Version!, new(Edited, [Callback]))!;
        probe.Armed = true;
        var first = test.ExecuteAsync(cloneFirst ? clone : edit);
        var second = await QueueAsync(test, probe, first, () => test.ExecuteOnAsync(peer, cloneFirst ? edit : clone, Guid.NewGuid(), test.Token));
        probe.RequireOutcome();
        var copied = cloneFirst ? await first : second;
        var edited = cloneFirst ? second : await first;
        Assert.Equal(200, edited.Status);
        Assert.Equal(Edited, edited.Application!.DisplayName);
        var committedSource = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id);
        Assert.Equal(Edited, committedSource.Display);
        Assert.Equal([Callback], committedSource.Redirects);
        Assert.Equal(2, committedSource.Policy.Revision);
        Assert.Equal(cloneFirst ? 201 : 412, copied.Status);
        if (cloneFirst)
        {
            Assert.Equal(clone.DisplayName, copied.Application!.DisplayName);
            Assert.Equal(source.Application.RedirectUris, copied.Application.RedirectUris);
            await using var scope = test.Scope();
            var detail = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationCloneAuditDetail>().AsNoTracking().SingleAsync(test.Token);
            Assert.Equal(source.Version, detail.SourceVersion);
        }
        else
        {
            Assert.Null(copied.Credential);
            await RequireAbsentAsync(test, clone.ClientId!);
        }
    }

    /// <summary>Catalogue edits committed while cloning waits are assessed at snapshot acquisition: eligible changes are copied and disabled scopes deny.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CloneWaitingBehindCatalogueEdit_When_CurrentEligibilityIsObserved_Then_EligibleChangesAreAcceptedAndDisabledScopesDeny(string provider, bool enabled)
    {
        var probe = new GrantLifecycleRaceProbe(winner: true);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, probe);
        await using var peer = test.Peer(provider);
        var selected = (await ApplicationScopeEvidence.SeedAsync(test))[0];
        var source = await test.ExecuteAsync(test.Create());
        source = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(source.Application!.Id, source.Version!, new([selected.Name]))!);
        Assert.Equal(200, source.Status);
        var sourceBefore = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        var input = new ScopeInput(selected.Name, Edited, selected.Kind, enabled, selected.ConsentText, selected.AudienceId, selected.Disclosure);
        var command = CatalogueIntent.Edit(selected.Id, selected.Version, input)!;
        probe.Armed = true;
        var changing = CatalogueMutationTests.ExecuteAsync(test, command);
        var copied = await QueueAsync(test, probe, changing, () => test.ExecuteOnAsync(peer,
            ApplicationCloneAtomicityTests.Clone(test, source.Application!.Id, source.Version!), Guid.NewGuid(), test.Token));
        probe.RequireOutcome();
        Assert.Equal(200, (await changing).Status);
        Assert.Equal(enabled ? 201 : 409, copied.Status);
        var sourceAfter = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        Assert.Equal(sourceBefore.NativeDigest, sourceAfter.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(sourceBefore.Policy), ApplicationUpdateSnapshot.Hash(sourceAfter.Policy));
        if (enabled)
        {
            Assert.Equal((await ApplicationScopeEvidence.ReadAsync(test, source.Application!.Id)).Permissions,
                (await ApplicationScopeEvidence.ReadAsync(test, copied.Application!.Id)).Permissions);
        }
        else
        {
            Assert.Null(copied.Credential);
            await RequireAbsentAsync(test, ApplicationCloneAtomicityTests.Clone(test, source.Application!.Id, source.Version!).ClientId!);
        }
    }

    /// <summary>Observes denied destinations and provenance through a fresh native scope, independent of the command response.</summary>
    private static async Task RequireAbsentAsync(ApplicationMutationFixture test, string client)
    {
        await using var scope = test.Scope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(client, test.Token));
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.Empty(await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == CloneEvent).ToArrayAsync(test.Token));
    }

    /// <summary>Always releases the held native commit and joins both operations, including when independent wait observation fails.</summary>
    private static async Task<ApplicationMutationResult> QueueAsync(ApplicationMutationFixture test, GrantLifecycleRaceProbe probe, Task first,
        Func<Task<ApplicationMutationResult>> start)
    {
        Task<ApplicationMutationResult>? second = null;
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(probe.Committing.Task, first).WaitAsync(TimeSpan.FromSeconds(WatchdogSeconds), test.Token);
            Assert.True(probe.Committing.Task.IsCompletedSuccessfully);
            var holder = await probe.Committing.Task;
            second = start();
            await DatabaseWaiters.ObserveOwnedAsync(test.Scenario.Account.Journey.Lease, holder, GovernanceUpdate, 1, test.Token);
            Assert.False(second.IsCompleted);
        }
        catch (Exception error) { failures.Add(error); }
        finally { probe.Release.TrySetResult(); }
        var joined = Task.WhenAll(first, (Task?)second ?? Task.CompletedTask);
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
        return await second!;
    }
}
