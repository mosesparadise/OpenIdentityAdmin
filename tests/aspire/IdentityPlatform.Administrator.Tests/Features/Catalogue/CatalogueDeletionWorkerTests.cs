using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Exercises actual production hosted polling and provider-observed competing workers without retaining live approval.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionWorkerTests(BootstrapFixture fixture)
{
    private const int Contenders = 2;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Two independent hosted workers must actually wait on governance, then commit only one terminal deletion and audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedDeletion_When_TwoHostedWorkersActuallyContend_Then_OneDurableTerminalTransitionCommits(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var lease = test.Scenario.Account.Journey.Lease;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using var first = GrantWorkerTests.CreateHost(provider, lease.RuntimeConnection, test.Clock);
        await using var second = GrantWorkerTests.CreateHost(provider, lease.RuntimeConnection, test.Clock);
        await using var held = await GovernanceLock.AcquireAsync(lease);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await first.StartAsync(deadline.Token);
            await second.StartAsync(deadline.Token);
            await held.ObserveOwnedAsync(lease, Contenders, deadline.Token);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await held.DisposeAsync(); }
        if (failure is null)
        {
            await ObserveAsync(test, accepted.Id, deadline.Token);
        }
        await first.StopAsync(deadline.Token);
        await second.StopAsync(deadline.Token);
        failure?.Throw();
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.None);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await database.Set<CatalogueDeletion>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.Equal(3, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetId == intent.Id, test.Token));
    }

    /// <summary>Observes persisted status without retries of admission or deletion and fails under one bounded test deadline.</summary>
    private static async Task ObserveAsync(ApplicationMutationFixture test, Guid id, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var job = await CatalogueDeletionTest.ReadAsync(test, id);
            Assert.NotEqual(CatalogueDeletionState.Rejected, job.State);
            if (job.State == CatalogueDeletionState.Succeeded)
            {
                return;
            }
            await Task.Delay(ObservationInterval, token);
        }
    }
}
