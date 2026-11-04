using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Injects faults only after a real native old-hash upgrade has started; it never replaces the hasher or user store.</summary>
internal sealed class ManagedPasswordUpgradeFault(ManagedPasswordUpgradeDefect defect) : SaveChangesInterceptor
{
    internal string? OriginalHash
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }
    internal bool MutationCompleted
    {
        get; private set;
    }
    internal bool Disabled
    {
        get; set;
    }

    /// <summary>Registers the same interceptor on the actual issuer database while leaving fixture setup unarmed.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Changes the real persisted concurrency predicate before native EF Update, causing its actual failed IdentityResult.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (defect == ManagedPasswordUpgradeDefect.Concurrency && Candidate(eventData.Context!) is { } user)
        {
            Fired = true;
            Assert.Equal(1, await eventData.Context!.Set<PlatformUser>().Where(value => value.Id == user.Id)
                .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.ConcurrencyStamp, Guid.NewGuid().ToString()), cancellationToken));
            MutationCompleted = true;
        }
        return result;
    }

    /// <summary>Changes only a schema-valid tracked or independently persisted stamp after the native upgrade really saved.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (defect is not (ManagedPasswordUpgradeDefect.TrackedStamp or ManagedPasswordUpgradeDefect.PersistedStamp) || Candidate(eventData.Context!) is not { } user)
        {
            return result;
        }
        Fired = true;
        var changed = Guid.NewGuid().ToString();
        if (defect == ManagedPasswordUpgradeDefect.TrackedStamp)
        {
            user.SecurityStamp = changed;
        }
        else
        {
            Assert.Equal(1, await eventData.Context!.Set<PlatformUser>().Where(value => value.Id == user.Id)
                .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.SecurityStamp, changed), cancellationToken));
        }
        MutationCompleted = true;
        return result;
    }

    /// <summary>Selects the genuine native-generated hash change, never an unrelated setup or credential operation.</summary>
    private PlatformUser? Candidate(DbContext database) => Disabled || Fired || OriginalHash is null ? null :
        database.ChangeTracker.Entries<PlatformUser>().SingleOrDefault(entry => entry.Entity.PasswordHash != OriginalHash)?.Entity;
}

/// <summary>Names the unmodified native upgrade and independent persistence corruption/failure controls.</summary>
public enum ManagedPasswordUpgradeDefect
{
    None, TrackedStamp, PersistedStamp, Concurrency
}
