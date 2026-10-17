using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Aborts live transport independently of native cancellation while retaining the request delegate.</summary>
internal sealed class ProtocolResponseDeadline : IAsyncDisposable
{
    private readonly ITimer timer;
    private int expired;
    internal bool Expired => Volatile.Read(ref expired) != 0;
    /// <summary>Starts the original monotonic remaining budget, never a fresh response budget.</summary>
    internal ProtocolResponseDeadline(HttpContext context, ProtocolStorageRequest request)
    {
        request.RequireCurrent();
        var remaining = request.Remaining;
        timer = request.Clock.CreateTimer(_ =>
        {
            Interlocked.Exchange(ref expired, 1);
            request.Fail();
            context.Abort();
        }, null, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }
    /// <summary>Joins any in-flight abort callback before the server may reuse the HTTP context.</summary>
    public ValueTask DisposeAsync() => timer.DisposeAsync();
}
