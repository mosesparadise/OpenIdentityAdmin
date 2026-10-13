using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Changes only product time after native proof reservation, immediately before the real target write.</summary>
internal sealed class GovernanceTimeBoundary : SaveChangesInterceptor
{
    private const int ExpirySeconds = 300;
    private const int RollbackSeconds = -1;
    private GovernanceScenario? scenario;
    private string? target;
    private string? role;
    private int replayCount;
    private bool expire;
    internal bool Observed
    {
        get; private set;
    }

    /// <summary>Captures the independent pre-command replay count and exact membership target after fixture preparation.</summary>
    internal void Arm(GovernanceScenario owner, string userId, string roleId, int replay, bool expires)
    {
        scenario = owner;
        target = userId;
        role = roleId;
        replayCount = replay;
        expire = expires;
    }

    /// <summary>Requires actual persisted native proof in the same transaction before shifting the owned policy clock.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (scenario is not { } owner || Observed || eventData.Context is not PlatformDbContext database ||
            !database.ChangeTracker.Entries<IdentityUserRole<string>>().Any(entry => entry.State == EntityState.Added &&
                entry.Entity.UserId == target && entry.Entity.RoleId == role))
        {
            return result;
        }
        Assert.NotNull(database.Database.CurrentTransaction);
        Assert.Equal(replayCount + 1, await database.Set<AdministratorReplay>().AsNoTracking()
            .CountAsync(row => row.UserId == owner.ActorId, cancellationToken));
        Observed = true;
        owner.Journey.Clock.Now = owner.Journey.Clock.Now.AddSeconds(expire ? ExpirySeconds : RollbackSeconds);
        return result;
    }
}
