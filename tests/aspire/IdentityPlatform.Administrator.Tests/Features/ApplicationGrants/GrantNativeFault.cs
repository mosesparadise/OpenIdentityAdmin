using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Observes or rejects an actual native token save without changing production managers or supplying an authorization callback.</summary>
internal sealed class GrantNativeFault(bool concurrency = false, CancellationTokenSource? cancellation = null) : SaveChangesInterceptor
{
    private const string Revoked = "revoked";
    internal bool Armed
    {
        get; set;
    }
    internal bool Observed
    {
        get; private set;
    }

    /// <summary>Forces the real manager's concurrency-failure result before its selected native write can commit.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (concurrency && Selected(eventData.Context))
        {
            Armed = false;
            Observed = true;
            throw new DbUpdateConcurrencyException();
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Records the actual persisted native transition before a later audit denial or owned cancellation forces rollback.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Selected(eventData.Context))
        {
            Armed = false;
            Observed = true;
            cancellation?.Cancel();
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Targets only the selected grant family's real revoked token save after test admission has completed.</summary>
    private bool Selected(DbContext? database) => Armed && database is not null &&
        database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Any(row => row.Entity.Status == Revoked);
}
