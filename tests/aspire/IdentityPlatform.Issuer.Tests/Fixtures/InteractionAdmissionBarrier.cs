using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Rendezvous before the first correlation lookup, never after the budget row is locked.
public sealed class InteractionAdmissionBarrier(CancellationToken deadline) : DbCommandInterceptor
{
    private readonly ConcurrentDictionary<Guid, byte> contexts = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => contexts.Count;

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && command.CommandText.Contains("LoginInteraction", StringComparison.Ordinal) &&
            command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && contexts.TryAdd(context.ContextId.InstanceId, 0))
        {
            if (contexts.Count == 2)
            {
                ready.TrySetResult();
            }
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(deadline, cancellationToken);
            await ready.Task.WaitAsync(waiting.Token);
        }
        return result;
    }
}
