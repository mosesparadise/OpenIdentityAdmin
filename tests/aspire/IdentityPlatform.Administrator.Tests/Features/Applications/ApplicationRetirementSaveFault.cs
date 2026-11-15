using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Changes an actually persisted terminal policy or receipt inside the owned transaction after the combined save, never before staging.</summary>
internal sealed class ApplicationRetirementSaveFault(bool receipt) : SaveChangesInterceptor
{
    private const string CorruptHash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
    internal bool Armed
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }

    /// <summary>Marks evidence only after the exact new receipt exists and one SQL row was changed; unrelated save failures cannot satisfy the oracle.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!Armed || eventData.Context is not PlatformDbContext database ||
            database.ChangeTracker.Entries<ApplicationRetirement>().Select(row => row.Entity).SingleOrDefault() is not { } row)
        {
            return result;
        }
        Armed = false;
        Assert.True(await database.Set<ApplicationRetirement>().AsNoTracking().AnyAsync(value => value.ApplicationId == row.ApplicationId, cancellationToken));
        var affected = receipt
            ? await database.Set<ApplicationRetirement>().Where(value => value.ApplicationId == row.ApplicationId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.RequestHash, CorruptHash), cancellationToken)
            : await database.Set<ApplicationPolicy>().Where(value => value.ApplicationId == row.ApplicationId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.Retired, false), cancellationToken);
        Assert.Equal(1, affected);
        Fired = true;
        return result;
    }
}
