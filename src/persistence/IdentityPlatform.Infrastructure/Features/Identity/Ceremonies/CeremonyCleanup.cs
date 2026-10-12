using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Reclaims bounded terminal metadata using the same admission/browser/account order as authentication.</summary>
internal sealed class CeremonyCleanup(PlatformDbContext database, NativeOperation operation, CeremonyClock clock)
{
    /// <summary>Attempts four independently atomic ordered category batches within one shared five-second deadline.</summary>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        await using var connection = new CeremonyConnection(database);
        await connection.OpenAsync(deadline.Token);
        await ObserveBacklogAsync(deadline.Token);
        var removed = await InteractionsAsync(deadline.Token);
        removed += await SessionsAsync(deadline.Token);
        removed += await BrowsersAsync(deadline.Token);
        removed += await ReplaysAsync(deadline.Token);
        return removed;
    }

    /// <summary>Measures eligible metadata counts without decrypting, identifying users or releasing retained capacity.</summary>
    private async Task ObserveBacklogAsync(CancellationToken token)
    {
        var now = clock.Now;
        var idle = now - AdministratorPolicy.IdleSeconds;
        var before = now - AdministratorPolicy.ReplaySeconds;
        CeremonyMetrics.RecordBacklog(CeremonyMetrics.Interactions, await database.Set<AdministratorInteraction>()
            .CountAsync(row => row.State == AdministratorPolicy.Consumed || row.ExpiresAt <= now, token));
        CeremonyMetrics.RecordBacklog(CeremonyMetrics.Sessions, await database.Set<AdministratorSession>()
            .CountAsync(row => row.Revoked || row.ExpiresAt <= now || row.LastActivityAt <= idle, token));
        CeremonyMetrics.RecordBacklog(CeremonyMetrics.Browsers, await database.Set<AdministratorBrowserBudget>()
            .CountAsync(row => row.ExpiresAt <= now && !database.Set<AdministratorInteraction>().Any(interaction => interaction.BrowserBudgetId == row.Id), token));
        CeremonyMetrics.RecordBacklog(CeremonyMetrics.Replays, await database.Set<AdministratorReplay>()
            .CountAsync(row => row.AcceptedAt < before && row.ExpiresAt < now, token));
    }

    /// <summary>Discovers a bounded candidate set, acquires all dependencies in order and rechecks terminal state before deletion.</summary>
    private async Task<int> InteractionsAsync(CancellationToken token)
    {
        var now = clock.Now;
        var candidates = await database.Set<AdministratorInteraction>().AsNoTracking()
            .Where(row => row.State == AdministratorPolicy.Consumed || row.ExpiresAt <= now)
            .OrderBy(row => row.Id).Take(AdministratorPolicy.CleanupRows).ToArrayAsync(token);
        if (candidates.Length == 0)
        {
            return 0;
        }
        var plan = new CeremonyLockPlan(false, true, candidates.Select(row => row.BrowserBudgetId).ToArray(), candidates.Select(row => row.UserId).ToArray());
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(database, operation, plan, token);
        var ids = candidates.Select(row => row.Id).ToArray();
        now = clock.Now;
        var removed = await database.Set<AdministratorInteraction>().Where(row => ids.Contains(row.Id) &&
            (row.State == AdministratorPolicy.Consumed || row.ExpiresAt <= now)).ExecuteDeleteAsync(transaction.Token);
        await transaction.CommitMaintenanceAsync();
        return removed;
    }

    /// <summary>Deletes expired or revoked sessions only while retained capacity is serialized.</summary>
    private async Task<int> SessionsAsync(CancellationToken token)
    {
        var now = clock.Now;
        var idle = now - AdministratorPolicy.IdleSeconds;
        var candidates = await database.Set<AdministratorSession>().AsNoTracking()
            .Where(row => row.Revoked || row.ExpiresAt <= now || row.LastActivityAt <= idle)
            .OrderBy(row => row.Id).Take(AdministratorPolicy.CleanupRows).ToArrayAsync(token);
        if (candidates.Length == 0)
        {
            return 0;
        }
        var plan = new CeremonyLockPlan(false, true, [], candidates.Select(row => row.UserId).ToArray());
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(database, operation, plan, token);
        var ids = candidates.Select(row => row.Id).ToArray();
        now = clock.Now;
        idle = now - AdministratorPolicy.IdleSeconds;
        var removed = await database.Set<AdministratorSession>().Where(row => ids.Contains(row.Id) &&
            (row.Revoked || row.ExpiresAt <= now || row.LastActivityAt <= idle)).ExecuteDeleteAsync(transaction.Token);
        await transaction.CommitMaintenanceAsync();
        return removed;
    }

    /// <summary>Deletes only expired unreferenced budgets; an admission cannot race its retained-count release.</summary>
    private async Task<int> BrowsersAsync(CancellationToken token)
    {
        var now = clock.Now;
        var ids = await database.Set<AdministratorBrowserBudget>().AsNoTracking().Where(row => row.ExpiresAt <= now &&
            !database.Set<AdministratorInteraction>().Any(interaction => interaction.BrowserBudgetId == row.Id))
            .OrderBy(row => row.Id).Take(AdministratorPolicy.CleanupRows).Select(row => row.Id).ToArrayAsync(token);
        if (ids.Length == 0)
        {
            return 0;
        }
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(database, operation, new(false, true, ids, []), token);
        now = clock.Now;
        var removed = await database.Set<AdministratorBrowserBudget>().Where(row => ids.Contains(row.Id) && row.ExpiresAt <= now &&
            !database.Set<AdministratorInteraction>().Any(interaction => interaction.BrowserBudgetId == row.Id)).ExecuteDeleteAsync(transaction.Token);
        await transaction.CommitMaintenanceAsync();
        return removed;
    }

    /// <summary>Deletes at most one hundred replay rows total while retaining every account watermark and all audit evidence.</summary>
    private async Task<int> ReplaysAsync(CancellationToken token)
    {
        var before = clock.Now - AdministratorPolicy.ReplaySeconds;
        var now = clock.Now;
        var users = await database.Set<AdministratorReplay>().AsNoTracking().Where(row => row.AcceptedAt < before && row.ExpiresAt < now)
            .OrderBy(row => row.Id).Take(AdministratorPolicy.CleanupRows).Select(row => row.UserId).ToArrayAsync(token);
        if (users.Length == 0)
        {
            return 0;
        }
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(database, operation, new(false, false, [], users), token);
        var removed = 0;
        foreach (var userId in users.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var state = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == userId, transaction.Token);
            removed += await CeremonyPruning.ReplayAsync(database, transaction, state, clock, AdministratorPolicy.CleanupRows - removed);
            if (removed == AdministratorPolicy.CleanupRows)
            {
                break;
            }
        }
        await transaction.CommitMaintenanceAsync();
        return removed;
    }
}
