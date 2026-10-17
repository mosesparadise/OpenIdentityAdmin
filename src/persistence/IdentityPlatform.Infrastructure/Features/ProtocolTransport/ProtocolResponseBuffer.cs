using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Retains bounded protocol bytes and never forwards a write to the network.</summary>
internal sealed class ProtocolResponseBuffer(ProtocolStorageRequest request, Func<CancellationToken, Task> start, ProtocolResponseActivity activity) : Stream
{
    private readonly MemoryStream buffer = new();
    private readonly object sync = new();
    internal bool Started
    {
        get; set;
    }
    internal ReadOnlyMemory<byte> Bytes => buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => buffer.Length;
    public override long Position
    {
        /// <summary>Reports the current staged byte position.</summary>
        get => buffer.Position;
        /// <summary>Rejects response rewinding even if the producer catches the error.</summary>
        set => throw Reject();
    }
    /// <summary>Checks cancellation and callbacks before retaining an asynchronous write.</summary>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        using var admission = activity.Enter();
        try
        {
            await start(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Write(value.Span);
        }
        catch { request.Fail(); throw; }
    }
    /// <summary>Routes the legacy asynchronous stream API to the same ownership guard.</summary>
    public override Task WriteAsync(byte[] value, int offset, int count, CancellationToken cancellationToken) => WriteAsync(value.AsMemory(offset, count), cancellationToken).AsTask();
    /// <summary>Retains only already-started synchronous writes; never blocks on asynchronous callbacks.</summary>
    public override void Write(ReadOnlySpan<byte> value)
    {
        using var admission = activity.Enter();
        request.RequireCurrent();
        lock (sync)
        {
            if (!Started || value.Length > ProtocolResponseLimits.BodyBytes - buffer.Length)
            {
                throw Reject();
            }
            buffer.Write(value);
        }
    }
    /// <summary>Routes synchronous array writes through the bounded buffer.</summary>
    public override void Write(byte[] value, int offset, int count) => Write(value.AsSpan(offset, count));
    /// <summary>Flushes only the in-memory response after verifying original ownership.</summary>
    public override void Flush() => request.RequireCurrent();
    /// <summary>Runs start callbacks for asynchronous flush without publishing network output.</summary>
    public override Task FlushAsync(CancellationToken cancellationToken) => start(cancellationToken);
    /// <summary>Rejects unsupported response reads.</summary>
    public override int Read(byte[] value, int offset, int count) => throw Reject();
    /// <summary>Rejects rewinding staged protocol output.</summary>
    public override long Seek(long offset, SeekOrigin origin) => throw Reject();
    /// <summary>Rejects replacement or truncation of staged output.</summary>
    public override void SetLength(long value) => throw Reject();
    /// <summary>Disposes staging memory only after the live request has joined all actual work.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }
        base.Dispose(disposing);
    }
    /// <summary>Ensures swallowed unsupported operations cannot produce an accepted response.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
