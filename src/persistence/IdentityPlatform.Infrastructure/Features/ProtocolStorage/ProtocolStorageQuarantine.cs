using System.Collections.Concurrent;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Retains and observes timed-out cleanup tasks with their entire service scope until actual completion.</summary>
internal static class ProtocolStorageQuarantine
{
    private static readonly ConcurrentDictionary<Task, byte> Pending = new();

    /// <summary>Roots the original cleanup operation and observes any eventual exception without launching duplicate disposal.</summary>
    internal static void Retain(Task cleanup)
    {
        Pending.TryAdd(cleanup, 0);
        _ = cleanup.ContinueWith(completed =>
        {
            _ = completed.Exception;
            Pending.TryRemove(completed, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
