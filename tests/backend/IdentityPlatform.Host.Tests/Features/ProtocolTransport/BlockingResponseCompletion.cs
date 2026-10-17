using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Blocks actual completion while forwarding every other operation to Kestrel's native body feature.</summary>
internal sealed class BlockingResponseCompletion(IHttpResponseBodyFeature inner,
    TaskCompletionSource entered, TaskCompletionSource release) : IHttpResponseBodyFeature
{
    private int completions;

    /// <summary>Counts actual terminal completion admission.</summary>
    internal int Completions => Volatile.Read(ref completions);

    /// <summary>Preserves native response writes.</summary>
    public Stream Stream => inner.Stream;

    /// <summary>Preserves native response pipe writes.</summary>
    public PipeWriter Writer => inner.Writer;

    /// <summary>Forwards native buffering behavior.</summary>
    public void DisableBuffering() => inner.DisableBuffering();

    /// <summary>Starts the native response without changing cancellation.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default) => inner.StartAsync(cancellationToken);

    /// <summary>Forwards file sending without altering the native feature.</summary>
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) =>
        inner.SendFileAsync(path, offset, count, cancellationToken);

    /// <summary>Retains terminal framing until explicitly released, regardless of transport abort.</summary>
    public async Task CompleteAsync()
    {
        Interlocked.Increment(ref completions);
        entered.TrySetResult();
        await release.Task;
        await inner.CompleteAsync();
    }
}
