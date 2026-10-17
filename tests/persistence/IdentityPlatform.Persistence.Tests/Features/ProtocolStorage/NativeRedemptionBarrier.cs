using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Pauses native redemption after its row has been fully read and before the actual EF update is sent.</summary>
internal sealed class NativeRedemptionBarrier : SaveChangesInterceptor
{
    private Gate? current;

    /// <summary>Arms one exact synthetic token; unrelated fixture saves and subsequent operations remain ordinary.</summary>
    internal Gate Arm(string identifier)
    {
        var gate = new Gate(identifier);
        Assert.Null(Interlocked.Exchange(ref current, gate));
        return gate;
    }

    /// <summary>Releases all native readers before the test exposes the stale tracked entity to a competing bulk revocation.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var gate = current;
        if (gate is not null && eventData.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>()
            .Any(entry => entry.State == EntityState.Modified && entry.Entity.Id == gate.Identifier && entry.Entity.Status == OpenIddictConstants.Statuses.Redeemed))
        {
            Assert.Same(gate, Interlocked.Exchange(ref current, null));
            gate.Reached.TrySetResult(NativeLockObservation.Session(eventData.Context.Database.GetDbConnection()));
            await gate.Release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Retains a single pre-update barrier without holding a reader or inserting a product hook.</summary>
    internal sealed class Gate(string identifier)
    {
        internal string Identifier => identifier;
        internal TaskCompletionSource<int> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
