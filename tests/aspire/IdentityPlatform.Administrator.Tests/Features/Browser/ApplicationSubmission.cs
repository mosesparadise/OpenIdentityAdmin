using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Passively counts one exact UI mutation without intercepting requests, response streams or browser cancellation.</summary>
internal sealed class ApplicationSubmission : IDisposable
{
    private readonly IPage page;
    private readonly string method;
    private readonly string target;
    private int count;

    /// <summary>Arms the observer before the original keyboard action; other routes and methods remain outside its exact scope.</summary>
    internal ApplicationSubmission(IPage page, string method, string target)
    {
        this.page = page;
        this.method = method;
        this.target = target;
        page.Request += Observe;
    }

    /// <summary>Requires one actual dispatch after the real UI has reached its asserted terminal state.</summary>
    internal void RequireOnce() => Assert.Equal(1, Volatile.Read(ref count));

    /// <summary>Retains only a count, never request headers, payloads, credentials or query values.</summary>
    private void Observe(object? sender, IRequest request)
    {
        if (request.Method == method && request.Url == target)
        {
            Interlocked.Increment(ref count);
        }
    }

    /// <summary>Detaches the observer on every success or failure path without altering the underlying request.</summary>
    public void Dispose() => page.Request -= Observe;
}
