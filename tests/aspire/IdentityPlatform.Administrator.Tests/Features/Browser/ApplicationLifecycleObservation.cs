using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Bounds private URL/history/console observations and records only native lifecycle facts outside test memory.</summary>
internal sealed class ApplicationLifecycleObservation : IAsyncDisposable
{
    private const int MaximumItems = 256;
    private const int MaximumCharacters = 262144;
    private const int MaximumArguments = 16;
    private const int JoinSeconds = 5;
    private const int PollMilliseconds = 20;
    private const int Unrequested = -1;
    private const string HiddenEvent = "hidden";
    private const string HideEvent = "hide";
    private const string ShowEvent = "show";
    private const string Report = "application-lifecycle native-hidden={0} pagehide={1} pageshow={2} bfcache-restores={3}";
    private const string TransitionFailure = "Required native lifecycle transition was not observed.";
    private const string TransitionReport = "application-lifecycle transition hidden={0}/{1} hides={2}/{3} shows={4}/{5}";
    private const string Canary = "synthetic-lifecycle-console-control";
    private const string State = "() => JSON.stringify({url:location.href,state:history.state})";
    private const string Seed = "value => console.debug(value, {nested:value})";
    private readonly IPage page;
    private readonly ApplicationLifecycleEvents events;
    private readonly ConcurrentQueue<string> values = [];
    private readonly ConcurrentBag<Task> pending = [];
    private int items;
    private int characters;
    private int failures;
    private int nativeHidden;
    private int nativeHides;
    private int nativeShows;
    private int persistedShows;
    private int lifecycleEvents;
    private int consoleEvents;
    internal int Hidden => Volatile.Read(ref nativeHidden);
    internal int Hides => Volatile.Read(ref nativeHides);
    internal int Shows => Volatile.Read(ref nativeShows);

    /// <summary>Attaches before application work; callbacks never write captured strings to test output.</summary>
    private ApplicationLifecycleObservation(IPage page)
    {
        this.page = page;
        events = new(Lifecycle, () => Interlocked.Increment(ref failures));
        page.Console += Console;
        page.PageError += Error;
        page.FrameNavigated += Navigated;
    }

    /// <summary>Records actual trusted browser lifecycle events without dispatching or altering any event.</summary>
    internal static async Task<ApplicationLifecycleObservation> CreateAsync(IPage page)
    {
        var observer = new ApplicationLifecycleObservation(page);
        try
        {
            await observer.events.AttachAsync(page);
            await page.EvaluateAsync(Seed, Canary);
            await observer.JoinAsync();
            Assert.Contains(observer.values, value => value.Contains(Canary, StringComparison.Ordinal));
            return observer;
        }
        catch { await observer.DisposeAsync(); throw; }
    }

    /// <summary>Accepts only finite native facts and fails capture rather than silently dropping excess observations.</summary>
    private void Lifecycle(string kind, bool trusted, bool persisted)
    {
        if (Interlocked.Increment(ref lifecycleEvents) > MaximumItems)
        {
            Interlocked.Increment(ref failures);
            return;
        }
        if (!trusted)
        {
            return;
        }
        if (kind == HiddenEvent)
        {
            Interlocked.Increment(ref nativeHidden);
        }

        if (kind == HideEvent)
        {
            Interlocked.Increment(ref nativeHides);
        }

        if (kind == ShowEvent)
        {
            Interlocked.Increment(ref nativeShows);
            if (persisted)
            {
                Interlocked.Increment(ref persistedShows);
            }
        }
    }

    /// <summary>Captures committed main-frame URLs, including native back and forward restoration.</summary>
    private void Navigated(object? sender, IFrame frame)
    {
        if (frame == page.MainFrame)
        {
            Add(frame.Url);
        }
    }

    /// <summary>Captures console text and joins bounded object-argument acquisition before any absence verdict.</summary>
    private void Console(object? sender, IConsoleMessage message)
    {
        if (Interlocked.Increment(ref consoleEvents) > MaximumItems)
        {
            Interlocked.Increment(ref failures);
            return;
        }
        Add(message.Text);
        pending.Add(ArgumentsAsync(message));
    }

    /// <summary>Reads actual console argument values privately; acquisition failure cannot become a clean secrecy result.</summary>
    private async Task ArgumentsAsync(IConsoleMessage message)
    {
        try
        {
            if (message.Args.Count > MaximumArguments)
            {
                Interlocked.Increment(ref failures);
                return;
            }
            foreach (var argument in message.Args)
            {
                Add((await argument.JsonValueAsync<JsonElement>()).GetRawText());
            }
        }
        catch { Interlocked.Increment(ref failures); }
    }

    /// <summary>Retains page errors only in bounded private memory.</summary>
    private void Error(object? sender, string error) => Add(error);

    /// <summary>Fails closed on overflow without printing or persisting raw observations.</summary>
    private void Add(string value)
    {
        if (Interlocked.Increment(ref items) > MaximumItems || Interlocked.Add(ref characters, value.Length) > MaximumCharacters)
        {
            Interlocked.Increment(ref failures);
            return;
        }
        values.Enqueue(value);
    }

    /// <summary>Joins every queued console read within one original independent budget.</summary>
    private async Task JoinAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(JoinSeconds));
        while (true)
        {
            var work = pending.ToArray();
            await Task.WhenAll(work).WaitAsync(deadline.Token);
            if (work.Length == pending.Count)
            {
                return;
            }
        }
    }

    /// <summary>Joins asynchronous native-event delivery without repeating navigation or manufacturing a visibility transition.</summary>
    internal async Task RequireTransitionsAsync(int? previousHidden = null, int? previousHides = null, int? previousShows = null)
    {
        var started = Stopwatch.GetTimestamp();
        while (previousHidden is { } hidden && Hidden <= hidden || previousHides is { } hides && Hides <= hides || previousShows is { } shows && Shows <= shows)
        {
            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(JoinSeconds))
            {
                TestContext.Current.TestOutputHelper?.WriteLine(TransitionReport, Hidden, previousHidden.GetValueOrDefault(Unrequested), Hides, previousHides.GetValueOrDefault(Unrequested), Shows, previousShows.GetValueOrDefault(Unrequested));
                Assert.Fail(TransitionFailure);
            }
            await Task.Delay(PollMilliseconds, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Checks observed URLs, current restored history state and console against actual private values; reports bfcache use without requiring it.</summary>
    internal async Task RequireAsync(string[] secrets)
    {
        Add(await page.EvaluateAsync<string>(State));
        await JoinAsync();
        Assert.Equal(0, Volatile.Read(ref failures));
        BrowserSecretOracle.RequireAbsent(values, secrets);
        TestContext.Current.TestOutputHelper?.WriteLine(Report, Hidden, Hides, Shows, Volatile.Read(ref persistedShows));
    }

    /// <summary>Detaches synchronous handlers and joins outstanding private reads before releasing browser ownership.</summary>
    public async ValueTask DisposeAsync()
    {
        page.Console -= Console;
        page.PageError -= Error;
        page.FrameNavigated -= Navigated;
        await events.DisposeAsync();
        await JoinAsync();
        while (values.TryDequeue(out _))
        {
        }
    }
}
