using System.Collections.Concurrent;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed class BootstrapCreationBarrier(CancellationToken deadline) : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Guid, byte> contexts = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstArrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => contexts.Count;
    public Task FirstArrival => firstArrival.Task;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        if (database.ChangeTracker.Entries<PlatformUser>().Any(value => value.State == EntityState.Added) &&
            contexts.TryAdd(database.ContextId.InstanceId, 0))
        {
            firstArrival.TrySetResult();
            if (contexts.Count == 2)
            {
                ready.TrySetResult();
            }
            // Only a synchronization signal is awaited; no database task is abandoned on cancellation.
            // Identity's UserManager can pass CancellationToken.None: bind to the test's own deadline too.
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(deadline, cancellationToken);
            await ready.Task.WaitAsync(waiting.Token);
        }
        return result;
    }
}
