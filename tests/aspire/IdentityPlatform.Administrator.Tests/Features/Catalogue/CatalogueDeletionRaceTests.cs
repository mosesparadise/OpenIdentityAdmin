using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires actual provider wait graphs before admission races and post-wait authority changes are allowed to proceed.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionRaceTests(BootstrapFixture fixture)
{
    private const int AcceptedStatus = 202;
    private const int ConflictStatus = 409;
    private const int AdmissionContenders = 2;
    private const int WorkerContender = 1;
    private const string GuidFormat = "D";
    private const string Expired = "expired";
    private const string Permission = "permission";
    private const string Stale = "stale";
    private const string PermissionName = "audiences.write";
    private const string AdmissionEvent = "CatalogueDeletionAdmitted";

    /// <summary>Same-key contenders return one original job; different-key contenders cannot create overlapping queued approval for the same target.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConcurrentAdmission_When_IndependentRequestsActuallyWait_Then_OneOriginalJobAndAuditAreRetained(string provider, bool sameKey)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var other = sameKey ? intent : CatalogueDeleteIntent.Create(intent.Resource, intent.Id, intent.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
        await using var peer = test.Peer(provider);
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var first = CatalogueDeletionTest.AdmitAsync(test, intent);
        var second = CatalogueDeletionTest.AdmitAsync(test, other, peer);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await held.ObserveOwnedAsync(lease, AdmissionContenders, test.Token);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await held.DisposeAsync(); }
        var results = await Task.WhenAll(first, second);
        failure?.Throw();
        Assert.Equal(sameKey ? 2 : 1, results.Count(value => value.Status == AcceptedStatus));
        Assert.Equal(sameKey ? 0 : 1, results.Count(value => value.Status == ConflictStatus));
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var job = await database.Set<CatalogueDeletion>().AsNoTracking().SingleAsync(test.Token);
        Assert.All(results.Where(value => value.Status == AcceptedStatus), value => Assert.Equal(job.Id, value.Job!.Id));
        Assert.Equal(intent.Id, job.TargetId);
        Assert.Equal(1, job.Revision);
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == AdmissionEvent, test.Token));
        Assert.True(await database.Set<ApiAudience>().AnyAsync(row => row.Id == intent.Id, test.Token));
    }

    /// <summary>Expiry, permission loss and a new target revision committed while the worker waits must be re-read after lock ownership.</summary>
    [Theory]
    [InlineData("PostgreSql", Expired, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Permission, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Stale, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Expired, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Permission, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Stale, Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedDeletion_When_AuthorityOrVersionChangesDuringObservedWait_Then_NoStaleDecisionDeletesTheTarget(string provider, string change)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var original = await CatalogueDeletionTest.ReadAsync(test, accepted.Id);
        var lease = test.Scenario.Account.Journey.Lease;
        await using var peer = test.Peer(provider);
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var processing = CatalogueDeletionTest.ProcessAsync(test, accepted.Id, peer);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await held.ObserveOwnedAsync(lease, WorkerContender, test.Token);
            await ChangeAsync(test, original, change);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await held.DisposeAsync(); }
        Assert.True(await processing);
        failure?.Throw();
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, change == Expired ? CatalogueDeletionFailure.Expired : change == Permission ? CatalogueDeletionFailure.Authority : CatalogueDeletionFailure.Stale);
    }

    /// <summary>Commits an independent fixture mutation only after the database has demonstrated that the worker is blocked before ownership.</summary>
    private static async Task ChangeAsync(ApplicationMutationFixture test, CatalogueDeletion job, string change)
    {
        if (change == Expired)
        {
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(job.ApprovalUntil);
            return;
        }
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (change == Permission)
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == PermissionName).ExecuteDeleteAsync(test.Token) > 0);
        }
        else
        {
            Assert.Equal(1, await database.Set<ApiAudience>().Where(row => row.Id == job.TargetId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Revision, row => row.Revision + 1), test.Token));
        }
    }
}
