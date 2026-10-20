using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Provides narrowly scoped test-owned committed interventions and independent observations without changing product registration policy.</summary>
internal static class NativeMutationState
{
    private const int MutationSeconds = 5;

    /// <summary>Reads only retained replay digests through a fresh context, never returning signed assertions or native payloads.</summary>
    internal static async Task<string[]> AssertionDigestsAsync(AdministratorProtocolScenario scenario)
    {
        await using var scope = scenario.Account.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorClientAssertionUse>()
            .AsNoTracking().OrderBy(row => row.Digest).Select(row => row.Digest).ToArrayAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Commits only a semantic profile revision under governance-before-profile locks; this is not an audited operator command.</summary>
    internal static async Task<(long Before, long After)> AdvanceRevisionAsync(AdministratorProtocolScenario scenario)
    {
        var before = await ReadProfileAsync(scenario);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(MutationSeconds));
        await using (var scope = scenario.Account.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(deadline.Token);
            await new CeremonyLocks(database).AcquireAsync(new CeremonyLockPlan(true, false, [], []), deadline.Token);
            Assert.Equal(1, await database.Set<AdministratorProtocolProfile>().Where(row => row.Id == AdministratorProtocolProfile.SingletonId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.LockRevision, row => row.LockRevision + 1)
                    .SetProperty(row => row.Revision, row => row.Revision + 1), deadline.Token));
            await transaction.CommitAsync(deadline.Token);
        }
        var after = await ReadProfileAsync(scenario);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.LockRevision + 1, after.LockRevision);
        Assert.True(after.Enabled && before.Enabled && after.Id == before.Id && after.ApplicationId == before.ApplicationId &&
            after.ScopeId == before.ScopeId && after.RedirectUri == before.RedirectUri &&
            after.PublicKeyFingerprint == before.PublicKeyFingerprint && after.ObservedAt == before.ObservedAt);
        return (before.Revision, after.Revision);
    }

    /// <summary>Bypasses tracked identity maps when checking the externally committed profile intervention.</summary>
    private static async Task<AdministratorProtocolProfile> ReadProfileAsync(AdministratorProtocolScenario scenario)
    {
        await using var scope = scenario.Account.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorProtocolProfile>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
    }
}
