using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Forwards the existing BFF response stream unchanged and observes only a successful single asynchronous publication.</summary>
internal sealed class ApplicationResponseWrite : Stream
{
    private readonly HttpContext http;
    private readonly IHttpResponseBodyFeature original;
    private readonly ApplicationResponseDates dates;
    private Stream Inner => original.Stream;

    /// <summary>Installs a request-scoped observer and retains the exact original feature for restoration before pipeline completion.</summary>
    internal ApplicationResponseWrite(HttpContext http, ApplicationResponseDates dates)
    {
        this.http = http;
        this.dates = dates;
        original = http.Features.Get<IHttpResponseBodyFeature>() ?? throw new InvalidOperationException();
        http.Response.Body = this;
    }

    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => Inner.CanWrite;
    public override long Length => Inner.Length;
    public override long Position
    {
        /// <summary>Returns the original stream position without observation.</summary>
        get => Inner.Position;
        /// <summary>Preserves the original stream's position-setting contract.</summary>
        set => Inner.Position = value;
    }
    /// <summary>Preserves the original stream's flush behavior.</summary>
    public override void Flush() => Inner.Flush();
    /// <summary>Preserves cancellation and completion of an ordinary flush.</summary>
    public override Task FlushAsync(CancellationToken cancellationToken) => Inner.FlushAsync(cancellationToken);
    /// <summary>Delegates the original stream's read contract without recording bytes.</summary>
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    /// <summary>Delegates the original stream's seek contract.</summary>
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    /// <summary>Delegates the original stream's length contract.</summary>
    public override void SetLength(long value) => Inner.SetLength(value);
    /// <summary>Forwards an unsupported synchronous write unchanged but cannot credit it as the expected asynchronous publication.</summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        dates.Reject();
        Inner.Write(buffer, offset, count);
    }

    /// <summary>Preserves one asynchronous write and its original token; failed writes invalidate evidence and rethrow the original failure.</summary>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await Inner.WriteAsync(buffer, cancellationToken);
        }
        catch { dates.Reject(); throw; }
        dates.Observe(buffer);
    }

    /// <summary>Routes the array overload through the same single forwarding operation.</summary>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Restores the original feature without disposing the server-owned stream.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            http.Features.Set(original);
        }

        base.Dispose(disposing);
    }
}
