using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins private context-wide network observations and retains only a final secrecy verdict outside test memory.</summary>
internal sealed class BrowserPrivateTraffic : IAsyncDisposable
{
    private const int MaximumItem = 2097152;
    private const int MaximumTotal = 16777216;
    private const int MaximumRequests = 256;
    private const int JoinSeconds = 30;
    private readonly IBrowserContext context;
    private readonly ConcurrentBag<Task> pending = [];
    private readonly ConcurrentQueue<string> values = [];
    private readonly ConcurrentQueue<string> diagnostics = [];
    private readonly ConcurrentBag<IPage> pages = [];
    private readonly object synchronization = new();
    private TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal BrowserFlowSecrets Flow { get; } = new();
    private int total;
    private int requests;
    private int responses;
    private int active;
    private int failed;
    private int detached;

    /// <summary>Attaches before login to include requests from every actual browser page, without routing or mocking traffic.</summary>
    internal BrowserPrivateTraffic(IBrowserContext context)
    {
        this.context = context;
        idle.SetResult();
        context.Request += Request;
        context.Response += Response;
        context.RequestFinished += Finished;
        context.RequestFailed += Failed;
        context.Page += Page;
        foreach (var page in context.Pages)
        {
            Page(context, page);
        }
    }

    /// <summary>Queues owned asynchronous header reads and bounds the actual request count.</summary>
    private void Request(object? sender, IRequest request)
    {
        lock (synchronization)
        {
            if (active++ == 0)
            {
                idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        if (Interlocked.Increment(ref requests) > MaximumRequests)
        {
            Interlocked.Increment(ref failed);
        }
        pending.Add(ReadRequestAsync(request));
    }

    /// <summary>Privately inspects actual URL, body and headers without retaining the request after completion.</summary>
    private async Task ReadRequestAsync(IRequest request)
    {
        try
        {
            Add(request.Url);
            Flow.Request(request.Url);
            Add(request.PostData ?? string.Empty);
            foreach (var header in await request.AllHeadersAsync())
            {
                Add(header.Key);
                Add(header.Value);
            }
        }
        catch (Exception) { Interlocked.Increment(ref failed); }
    }

    /// <summary>Queues an owned body/header observation for each actual browser response.</summary>
    private void Response(object? sender, IResponse response)
    {
        Interlocked.Increment(ref responses);
        pending.Add(ReadResponseAsync(response));
    }

    /// <summary>Joins response completion before reading bytes; native redirects and bodyless statuses still have all headers inspected.</summary>
    private async Task ReadResponseAsync(IResponse response)
    {
        try
        {
            Add(response.Url);
            foreach (var header in await response.AllHeadersAsync())
            {
                Add(header.Key);
                Add(header.Value);
            }
            foreach (var header in await response.HeadersArrayAsync())
            {
                Flow.Header(header.Name, header.Value);
            }
            if (response.Status is >= RedirectStart and < RedirectEnd or NoContent)
            {
                return;
            }
            await response.FinishedAsync();
            var body = await response.BodyAsync();
            if (body.Length > MaximumItem)
            {
                Interlocked.Increment(ref failed);
                return;
            }
            Add(Encoding.UTF8.GetString(body));
        }
        catch (Exception) { Interlocked.Increment(ref failed); }
    }

    /// <summary>Counts completed requests independently of response observer execution.</summary>
    private void Finished(object? sender, IRequest request)
    {
        lock (synchronization)
        {
            if (--active == 0)
            {
                idle.TrySetResult();
            }
        }
    }

    /// <summary>Fails closed on an incomplete positive-journey request without retaining its diagnostic text.</summary>
    private void Failed(object? sender, IRequest request)
    {
        Finished(sender, request);
        Interlocked.Increment(ref failed);
    }

    /// <summary>Retains a bounded private observation, failing closed rather than truncating possible secret exposure.</summary>
    private bool Add(string value)
    {
        if (value.Length > MaximumItem || Interlocked.Add(ref total, value.Length) > MaximumTotal)
        {
            Interlocked.Increment(ref failed);
            return false;
        }
        values.Enqueue(value);
        return true;
    }

    /// <summary>Detaches only after the completed browser journey, joins every queued observation, and scans actual server-only secrets retrospectively.</summary>
    internal async Task RequireAsync(string[] secrets)
    {
        await JoinAsync();
        Assert.Equal(0, Volatile.Read(ref active));
        Assert.Equal(0, Volatile.Read(ref failed));
        Assert.True(requests > 0 && responses > 0 && !values.IsEmpty);
        BrowserSecretOracle.RequireAbsent(values, secrets);
        BrowserSecretOracle.RequireAbsent(diagnostics, secrets.Concat(Flow.Require()).ToArray());
    }

    /// <summary>Requires every independently seeded actual network/diagnostic canary to have been acquired by the observer.</summary>
    internal async Task RequireProbesAsync(string[] canaries)
    {
        await JoinAsync();
        Assert.Equal(0, Volatile.Read(ref failed));
        Assert.True(canaries.All(canary => values.Any(value => BrowserSecretOracle.Contains(value, [canary]))));
    }

    /// <summary>Joins native response-header observations before requiring transient protocol/cookie capture.</summary>
    internal async Task<string[]> FlowAsync()
    {
        await JoinAsync();
        return Flow.Require();
    }

    /// <summary>Waits for actual request completion and a stable drained observer set before concluding the journey.</summary>
    private async Task JoinAsync()
    {
        while (true)
        {
            Task completion;
            lock (synchronization)
            {
                completion = idle.Task;
            }
            await completion.WaitAsync(TimeSpan.FromSeconds(JoinSeconds), TestContext.Current.CancellationToken);
            var tasks = pending.ToArray();
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(JoinSeconds), TestContext.Current.CancellationToken);
            lock (synchronization)
            {
                if (active == 0 && tasks.Length == pending.Count)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Cancels actual browser work on failed draining, then joins observers before detaching or releasing their owner.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await JoinAsync();
        }
        catch
        {
            try
            {
                await context.CloseAsync();
            }
            finally { await Task.WhenAll(pending.ToArray()); }
            throw;
        }
        finally { Detach(); }
    }

    /// <summary>Includes actual console and page errors from every page without persisting their text.</summary>
    private void Page(object? sender, IPage page)
    {
        pages.Add(page);
        page.Console += Console;
        page.PageError += Error;
    }

    /// <summary>Retains browser console text only in bounded test memory for exact secret scanning.</summary>
    private void Console(object? sender, IConsoleMessage message)
    {
        Diagnostic(message.Text);
        pending.Add(ConsoleArgumentsAsync(message));
    }

    /// <summary>Serializes actual console object arguments privately and joins all handle reads before browser disposal.</summary>
    private async Task ConsoleArgumentsAsync(IConsoleMessage message)
    {
        try
        {
            if (message.Args.Count > MaximumArguments)
            {
                Interlocked.Increment(ref failed);
                return;
            }
            foreach (var argument in message.Args)
            {
                var value = await argument.JsonValueAsync<JsonElement>();
                Diagnostic(value.GetRawText());
            }
        }
        catch (Exception) { Interlocked.Increment(ref failed); }
    }

    /// <summary>Keeps diagnostic observations separately so protocol transport values are forbidden here even when valid in HTTP.</summary>
    private void Diagnostic(string value)
    {
        if (Add(value))
        {
            diagnostics.Enqueue(value);
        }
    }

    /// <summary>Retains page error text privately without emitting raw browser diagnostics.</summary>
    private void Error(object? sender, string error) => Diagnostic(error);

    /// <summary>Removes all callbacks after joined completion, including pages created during the journey.</summary>
    private void Detach()
    {
        if (Interlocked.Exchange(ref detached, 1) != 0)
        {
            return;
        }
        context.Request -= Request;
        context.Response -= Response;
        context.RequestFinished -= Finished;
        context.RequestFailed -= Failed;
        context.Page -= Page;
        foreach (var page in pages)
        {
            page.Console -= Console;
            page.PageError -= Error;
        }
    }
    private const int RedirectStart = 300;
    private const int RedirectEnd = 400;
    private const int NoContent = 204;
    private const int MaximumArguments = 64;
}
