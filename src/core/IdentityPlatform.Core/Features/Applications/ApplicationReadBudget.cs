namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Bounds one complete management read without allowing nested work or clock rollback to renew its authority.</summary>
/// <example><code>using var budget = new ApplicationReadBudget(TimeProvider.System, requestAborted); await ReadAsync(budget.Token); budget.RequireCurrent();</code></example>
public sealed class ApplicationReadBudget : IDisposable
{
    public const int Seconds = 5;
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly DateTimeOffset wallStart;
    private readonly CancellationTokenSource cancellation;
    public CancellationToken Token => cancellation.Token;

    /// <summary>Links caller cancellation to one original five-second duration and captures monotonic and wall-clock bounds.</summary>
    /// <example><code>using var budget = new ApplicationReadBudget(TimeProvider.System, CancellationToken.None);</code></example>
    public ApplicationReadBudget(TimeProvider clock, CancellationToken token)
    {
        this.clock = clock;
        started = clock.GetTimestamp();
        wallStart = clock.GetUtcNow();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(Seconds));
    }

    /// <summary>Rejects cancellation, equality at deadline, monotonic expiry and wall-clock rollback immediately before publication.</summary>
    /// <example><code>budget.RequireCurrent(); // Call after all awaited reads and cleanup.</code></example>
    public void RequireCurrent()
    {
        Token.ThrowIfCancellationRequested();
        if (clock.GetUtcNow() < wallStart || clock.GetUtcNow() - wallStart >= TimeSpan.FromSeconds(Seconds) ||
            clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(Seconds))
        {
            throw new OperationCanceledException(Token);
        }
    }

    /// <summary>Releases the linked timer after owned work has actually completed.</summary>
    /// <example><code>using var budget = new ApplicationReadBudget(TimeProvider.System, requestAborted);</code></example>
    public void Dispose() => cancellation.Dispose();
}
