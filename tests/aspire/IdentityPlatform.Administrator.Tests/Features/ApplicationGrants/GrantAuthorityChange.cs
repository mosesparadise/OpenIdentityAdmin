using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Changes persisted authority after actual progress/audit save so only the worker's final authority reread can reject the staged batch.</summary>
internal sealed class GrantAuthorityChange(bool permission) : SaveChangesInterceptor
{
    private const string Grant = "grants.revoke";
    internal string? Actor
    {
        get; set;
    }
    internal bool Observed
    {
        get; private set;
    }
    internal GrantJobState? SavedState
    {
        get; private set;
    }

    /// <summary>Mutates once in the same native transaction, bypassing no database constraint and replacing no authorization result.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor || data.Context is not PlatformDbContext database ||
            !database.ChangeTracker.Entries<ApplicationGrantAudit>().Any())
        {
            return result;
        }
        var job = database.ChangeTracker.Entries<ApplicationGrantJob>().Single().Entity;
        Assert.True(job.State is GrantJobState.Processing or GrantJobState.Succeeded);
        Assert.NotNull(database.Database.CurrentTransaction);
        Actor = null;
        Observed = true;
        SavedState = job.State;
        if (permission)
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Grant).ExecuteDeleteAsync(cancellationToken) > 0);
            Assert.False(await database.RoleClaims.AnyAsync(row => row.ClaimValue == Grant, cancellationToken));
        }
        else
        {
            Assert.Equal(1, await database.Users.Where(row => row.Id == actor)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), cancellationToken));
            Assert.False(await database.Users.Where(row => row.Id == actor).Select(row => row.IsEnabled).SingleAsync(cancellationToken));
        }
        return result;
    }
}
