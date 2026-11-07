using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Records only finite grant intent fields and read counts from the actual browser, never cookies, antiforgery or bearer material.</summary>
internal sealed class GrantRecoveryTraffic : IDisposable
{
    internal const string Post = "POST";
    private const string Get = "GET";
    private const string Separator = "/";
    private readonly IPage page;
    private readonly string collection;
    private readonly ConcurrentQueue<GrantRecoveryIntent> posts = new();
    private int reads;
    internal GrantRecoveryIntent[] Posts => [.. posts];
    internal int Reads => Volatile.Read(ref reads);

    /// <summary>Observes only this exact application's grant endpoints while other browser traffic remains untouched.</summary>
    internal GrantRecoveryTraffic(IPage page, string collection)
    {
        this.page = page;
        this.collection = collection;
        page.Request += Observe;
    }

    /// <summary>Copies safe immutable intent fields synchronously without retaining browser request objects.</summary>
    private void Observe(object? sender, IRequest request)
    {
        if (request.Url != collection && !request.Url.StartsWith(collection + Separator, StringComparison.Ordinal))
        {
            return;
        }
        if (request.Method == Post)
        {
            posts.Enqueue(GrantRecoveryIntent.Read(request));
        }
        else if (request.Method == Get)
        {
            Interlocked.Increment(ref reads);
        }
    }

    /// <summary>Detaches the observer before the owning browser context is disposed.</summary>
    public void Dispose() => page.Request -= Observe;

}
