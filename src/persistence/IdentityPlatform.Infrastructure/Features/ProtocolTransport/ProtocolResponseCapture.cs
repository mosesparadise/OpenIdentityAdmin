using System.IO.Pipelines;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Implements the finite native protocol response surface without exposing live transport features.</summary>
internal sealed class ProtocolResponseCapture : IHttpResponseFeature, IHttpResponseBodyFeature, IDisposable
{
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolResponseActivity activity;
    private readonly object sync = new();
    private readonly ProtocolResponseHeaders headers;
    private readonly ProtocolResponseBuffer buffer;
    private readonly ProtocolResponseWriter writer;
    private readonly List<(Func<object, Task> Callback, object State)> starting = [];
    private readonly AsyncLocal<bool> inCallback = new();
    private Task? start;
    private int status = Microsoft.AspNetCore.Http.StatusCodes.Status200OK;
    private string? reason;
    public int StatusCode
    {
        /// <summary>Reads the staged HTTP status.</summary>
        get => status;
        /// <summary>Changes status only before the atomic response-start boundary.</summary>
        set
        {
            lock (sync)
            {
                RequireMutable();
                status = value;
            }
        }
    }
    public string? ReasonPhrase
    {
        /// <summary>Reads the staged bounded reason phrase.</summary>
        get => reason;
        /// <summary>Changes the bounded reason only before the atomic response-start boundary.</summary>
        set
        {
            lock (sync)
            {
                RequireMutable();
                if (value?.Length > ProtocolResponseLimits.HeaderBytes)
                {
                    throw Reject();
                }
                reason = value;
            }
        }
    }
    public Microsoft.AspNetCore.Http.IHeaderDictionary Headers
    {
        /// <summary>Exposes only the bounded staged header dictionary.</summary>
        get => headers;
        /// <summary>Rejects replacing the owned response component.</summary>
        set => throw Reject();
    }
    public Stream Body
    {
        /// <summary>Exposes only the retained response stream.</summary>
        get => buffer;
        /// <summary>Rejects replacing the owned response component.</summary>
        set => throw Reject();
    }
    public bool HasStarted => buffer.Started;
    public Stream Stream => buffer;
    public PipeWriter Writer => writer;
    internal ReadOnlyMemory<byte> Bytes => buffer.Bytes;

    /// <summary>Allocates bounded staging independent of the server's original response implementation.</summary>
    internal ProtocolResponseCapture(ProtocolStorageRequest request)
    {
        this.request = request;
        activity = new(request);
        headers = new(request);
        buffer = new(request, StartAsync, activity);
        writer = new(request, buffer, activity);
    }

    /// <summary>Retains bounded callbacks to execute before owned services are released.</summary>
    public void OnStarting(Func<object, Task> callback, object state)
    {
        lock (sync)
        {
            RequireMutable();
            if (start is not null || starting.Count >= ProtocolResponseLimits.Callbacks)
            {
                throw Reject();
            }
            starting.Add((callback, state));
        }
    }
    /// <summary>Rejects completion hooks whose scoped lifetime this finite protocol boundary cannot support.</summary>
    public void OnCompleted(Func<object, Task> callback, object state) => throw Reject();
    /// <summary>Rejects bypassing bounded staging with unbuffered streaming.</summary>
    public void DisableBuffering() => throw Reject();
    /// <summary>Rejects file streaming outside the fixed protocol response surface.</summary>
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken) => throw Reject();
    /// <summary>Starts only the staged response and joins original callbacks without publishing bytes.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        request.RequireCurrent();
        if (cancellationToken.IsCancellationRequested)
        {
            request.Fail();
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (inCallback.Value)
        {
            throw Reject();
        }
        lock (sync)
        {
            return start ??= StartCoreAsync();
        }
    }
    /// <summary>Completes the finite staged response without completing the live connection.</summary>
    public Task CompleteAsync() => FinishAsync();
    /// <summary>Flushes pipe output and executes header-only callbacks before publication checks.</summary>
    internal async Task FinishAsync()
    {
        await writer.FinishAsync();
        await StartAsync();
    }
    /// <summary>Joins an already-started callback task even when downstream throws or abandons its write.</summary>
    internal async Task JoinCallbacksAsync()
    {
        await activity.CloseAsync();
        if (start is not null)
        {
            await start;
        }
    }
    /// <summary>Runs reverse-order native callbacks once, while the owned child provider remains active.</summary>
    private async Task StartCoreAsync()
    {
        using var admission = activity.Enter();
        await Task.Yield();
        inCallback.Value = true;
        try
        {
            for (var index = starting.Count - 1; index >= 0; index--)
            {
                await starting[index].Callback(starting[index].State);
            }
            request.RequireCurrent();
            lock (sync)
            {
                headers.Freeze();
                buffer.Started = true;
            }
        }
        catch { request.Fail(); throw; }
        finally { inCallback.Value = false; }
    }
    /// <summary>Rejects status/header mutation after native start or original request failure.</summary>
    private void RequireMutable()
    {
        request.RequireCurrent();
        if (HasStarted)
        {
            throw Reject();
        }
    }
    /// <summary>Latches unsupported features even when downstream catches their exception.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
    /// <summary>Releases staging only after callbacks and actual owned work have completed.</summary>
    public void Dispose() => buffer.Dispose();
}
