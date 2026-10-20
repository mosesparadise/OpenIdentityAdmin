using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Testing;

/// <summary>Observes one original server request's cancellation before cleanup releases its test-only pre-processing pause.</summary>
internal sealed class NativeRequestPause(string path)
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private bool cancellationStarted;
    private bool premature;
    private int matches;
    internal Task Reached => reached.Task;
    internal Task Cancelled => cancelled.Task;

    /// <summary>Registers the live original token before readiness; cleanup always terminates the request instead of continuing into issuance.</summary>
    internal async Task<bool> TryPauseAsync(HttpContext http)
    {
        if (!string.Equals(http.Request.Method, HttpMethods.Post, StringComparison.Ordinal) ||
            !string.Equals(http.Request.Path.Value, path, StringComparison.Ordinal))
        {
            return false;
        }
        if (Interlocked.Increment(ref matches) != 1)
        {
            throw new InvalidOperationException();
        }
        var original = http.RequestAborted;
        original.ThrowIfCancellationRequested();
        using var registration = original.Register(RecordCancellation);
        reached.TrySetResult();
        try
        {
            await Task.WhenAny(cancelled.Task, release.Task);
        }
        finally { http.Abort(); }
        return true;
    }

    /// <summary>Rejects cancellation already observed before the test initiates its explicit client cancellation action.</summary>
    internal void BeginCancellation()
    {
        lock (sync)
        {
            if (!reached.Task.IsCompletedSuccessfully || cancelled.Task.IsCompleted || release.Task.IsCompleted || cancellationStarted)
            {
                throw new InvalidOperationException();
            }
            cancellationStarted = true;
        }
    }

    /// <summary>Classifies the original token's observation atomically against initiation, without treating cleanup as an initiating action.</summary>
    private void RecordCancellation()
    {
        lock (sync)
        {
            premature = !cancellationStarted;
            cancelled.TrySetResult();
        }
    }

    /// <summary>Credits only cancellation observed before test cleanup, while the actual caller still owns the intercepted request.</summary>
    internal void RequireCancelled()
    {
        lock (sync)
        {
            if (!cancellationStarted || premature || !cancelled.Task.IsCompletedSuccessfully || release.Task.IsCompleted)
            {
                throw new InvalidOperationException();
            }
        }
    }

    /// <summary>Unblocks server cleanup without signalling cancellation evidence or allowing native processing to resume.</summary>
    internal void Release() => release.TrySetResult();
}

/// <summary>Records an in-memory client's actual boundary abort while preserving normal request-token replacement and restoration.</summary>
internal sealed class NativeRequestLifetime(CancellationToken token) : IHttpRequestLifetimeFeature
{
    private int aborted;
    public CancellationToken RequestAborted { get; set; } = token;
    internal bool Aborted => Volatile.Read(ref aborted) != 0;

    /// <summary>Records transport termination without manufacturing cancellation of the independent HTTPS server token.</summary>
    public void Abort() => Interlocked.Exchange(ref aborted, 1);
}
