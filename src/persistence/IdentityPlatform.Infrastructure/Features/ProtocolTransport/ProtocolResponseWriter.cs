using System.IO.Pipelines;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Bounds pipe reservations before allocation and forwards flushes only to the staged stream.</summary>
internal sealed class ProtocolResponseWriter(ProtocolStorageRequest request, ProtocolResponseBuffer buffer, ProtocolResponseActivity activity) : PipeWriter
{
    private readonly byte[] bytes = new byte[ProtocolResponseLimits.BodyBytes];
    private int length;
    private bool completed;
    private int flushing;
    /// <summary>Reserves only memory within the fixed protocol response staging limit.</summary>
    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        RequireAvailable();
        if (sizeHint < 0 || sizeHint > bytes.Length - length || length == bytes.Length)
        {
            throw Reject();
        }
        return bytes.AsMemory(length);
    }
    /// <summary>Exposes the same bounded reservation as a span.</summary>
    public override Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    /// <summary>Commits only bytes within the current bounded reservation.</summary>
    public override void Advance(int count)
    {
        RequireAvailable();
        if (count < 0 || count > bytes.Length - length)
        {
            throw Reject();
        }
        length += count;
    }
    /// <summary>Flushes the real pending bytes while rejecting overlapping producer activity.</summary>
    public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        using var admission = activity.Enter();
        RequireAvailable();
        if (Interlocked.Exchange(ref flushing, 1) != 0)
        {
            throw Reject();
        }
        try
        {
            await buffer.WriteAsync(bytes.AsMemory(0, length), cancellationToken);
            length = 0;
            return new(false, false);
        }
        finally { Volatile.Write(ref flushing, 0); }
    }
    /// <summary>Completes a quiescent writer without silently discarding pending protocol bytes.</summary>
    public override void Complete(Exception? exception = null)
    {
        if (completed && exception is null)
        {
            return;
        }
        RequireAvailable();
        if (length != 0 || exception is not null)
        {
            throw Reject();
        }
        completed = true;
    }
    /// <summary>Asynchronously flushes pending bytes before completing the writer.</summary>
    public override async ValueTask CompleteAsync(Exception? exception = null)
    {
        if (completed && exception is null)
        {
            return;
        }
        if (exception is not null)
        {
            throw Reject();
        }
        await FlushAsync();
        Complete();
    }
    /// <summary>Rejects cancellation that could otherwise silently truncate a successful staged response.</summary>
    public override void CancelPendingFlush() => throw Reject();
    /// <summary>Finishes only writers not already completed by their native producer.</summary>
    internal ValueTask FinishAsync() => completed ? ValueTask.CompletedTask : CompleteAsync();
    /// <summary>Rejects stale, completed and concurrent producer operations before touching staging memory.</summary>
    private void RequireAvailable()
    {
        request.RequireCurrent();
        if (completed || Volatile.Read(ref flushing) != 0)
        {
            throw Reject();
        }
    }
    /// <summary>Marks every invalid pipe operation as terminal for response admission.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
