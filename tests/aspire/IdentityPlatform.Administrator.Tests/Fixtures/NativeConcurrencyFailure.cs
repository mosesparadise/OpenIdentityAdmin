using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Forces a real zero-row native user update after recovery redemption without replacing Identity's concurrency handling.</summary>
internal sealed class NativeConcurrencyFailure : SaveChangesInterceptor
{
    private const string StaleStamp = "synthetic-stale-original-concurrency-stamp";
    internal bool Armed
    {
        get; set;
    }
    internal bool ObservedClaim
    {
        get; private set;
    }
    internal bool ObservedConcurrency
    {
        get; private set;
    }

    /// <summary>Changes only EF's expected original stamp; the current/persisted native user remains unchanged until its real update fails.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && eventData.Context is PlatformDbContext database &&
            database.ChangeTracker.Entries<PlatformUser>().SingleOrDefault(entry => entry.State == EntityState.Modified) is { } user)
        {
            Armed = false;
            Assert.NotNull(database.Database.CurrentTransaction);
            ObservedClaim = await database.Set<AdministratorRecoveryCode>().AsNoTracking()
                .AnyAsync(row => row.RedeemedAt != null, cancellationToken);
            user.Property(value => value.ConcurrencyStamp).OriginalValue = StaleStamp;
        }
        return result;
    }

    /// <summary>Records the actual provider-backed optimistic-concurrency exception before the native store converts it to IdentityResult.</summary>
    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ObservedConcurrency |= eventData.Exception is DbUpdateConcurrencyException;
        return ValueTask.FromResult(result);
    }
}
