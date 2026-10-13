using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>targets actual native membership persistence after a real replay reservation, without replacing Identity concurrency handling.</summary>
internal sealed class GovernanceNativeConcurrency : SaveChangesInterceptor
{
    private const string StaleStamp = "synthetic-governance-stale-original-stamp";
    private const string MissingNativeBoundary = "The expected native governance persistence boundary was not present.";
    private string? actorId;
    private string? targetId;
    private string? roleId;
    private int replayCount;
    internal bool ObservedReplay
    {
        get; private set;
    }
    internal bool ObservedTarget
    {
        get; private set;
    }
    internal bool ObservedConcurrency
    {
        get; private set;
    }

    /// <summary>Arms only after fixture preparation; actor budget-reset updates cannot trigger the fault.</summary>
    internal void Arm(string actor, string target, string role, int originalReplayCount)
    {
        Assert.True(actor != target && originalReplayCount >= 0, MissingNativeBoundary);
        actorId = actor;
        targetId = target;
        roleId = role;
        replayCount = originalReplayCount;
    }

    /// <summary>Changes only EF's optimistic expected stamp before the real target membership save reaches the provider.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (targetId is not { } target || eventData.Context is not PlatformDbContext database)
        {
            return result;
        }
        var user = database.ChangeTracker.Entries<PlatformUser>()
            .SingleOrDefault(entry => entry.Entity.Id == target && entry.State == EntityState.Modified);
        if (user is null)
        {
            return result;
        }
        Assert.True(database.Database.CurrentTransaction is not null, MissingNativeBoundary);
        Assert.True(database.ChangeTracker.Entries<IdentityUserRole<string>>().Any(entry =>
            entry.State == EntityState.Added && entry.Entity.UserId == target && entry.Entity.RoleId == roleId), MissingNativeBoundary);
        ObservedReplay = await database.Set<AdministratorReplay>().AsNoTracking()
            .CountAsync(row => row.UserId == actorId, cancellationToken) == replayCount + 1;
        Assert.True(ObservedReplay, MissingNativeBoundary);
        targetId = null;
        ObservedTarget = true;
        user.Property(value => value.ConcurrencyStamp).OriginalValue = StaleStamp;
        return result;
    }

    /// <summary>Records a real provider-backed concurrency error before the native store returns failed IdentityResult.</summary>
    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ObservedConcurrency |= ObservedTarget && eventData.Exception is DbUpdateConcurrencyException;
        return ValueTask.FromResult(result);
    }
}
