namespace IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

/// <summary>Bounds actual local response writes before allocation can grow beyond the private child protocol budget.</summary>
internal sealed class BrowserDriverBody : MemoryStream
{
    internal const int Limit = 8192;

    /// <summary>Rejects oversized synchronous writes before mutating the captured response.</summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Require(count);
        base.Write(buffer, offset, count);
    }

    /// <summary>Bounds span writes used by local serializers.</summary>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Require(buffer.Length);
        base.Write(buffer);
    }

    /// <summary>Bounds byte writes independently of other overload dispatch.</summary>
    public override void WriteByte(byte value)
    {
        Require(1);
        base.WriteByte(value);
    }

    /// <summary>Bounds asynchronous array writes before delegating to the owned memory buffer.</summary>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Require(count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <summary>Bounds asynchronous memory writes used by the real HTTP result pipeline.</summary>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Require(buffer.Length);
        return base.WriteAsync(buffer, cancellationToken);
    }

    /// <summary>Rejects direct expansion beyond the same fixed response bound.</summary>
    public override void SetLength(long value)
    {
        if (value < 0 || value > Limit)
        {
            throw new InvalidOperationException();
        }
        base.SetLength(value);
    }

    /// <summary>Checks the current position so seeking cannot bypass the finite capture limit.</summary>
    private void Require(int count)
    {
        if (count < 0 || Position > Limit - count)
        {
            throw new InvalidOperationException();
        }
    }
}
