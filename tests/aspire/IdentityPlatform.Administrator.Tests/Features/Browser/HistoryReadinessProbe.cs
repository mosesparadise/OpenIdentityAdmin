using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Controls synthetic image delivery and retains only fixed phase/counter evidence for readiness failures.</summary>
internal sealed class HistoryReadinessProbe
{
    private const string Origin = "https://history.example.invalid";
    private const string Pending = "/pending";
    private const string RoutePattern = Origin + "/**";
    private const string Readiness = "document.readyState";
    private const string Complete = "complete";
    private const string CleanupFailure = "History probe route cleanup failed.";
    private const string SvgContentType = "image/svg+xml";
    private const string HtmlContentType = "text/html";
    private const string Image = "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/>";
    private const string HeldDocument = "<!doctype html><html><body><img src='/pending' alt=''>Safe control</body></html>";
    private const int IncompleteTimeoutMilliseconds = 250;
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(5);
    private static readonly CompositeFormat Evidence = CompositeFormat.Parse(
        "history-readiness elapsed-ms={0} requested={1} released={2} fulfilled={3} loads={4} safe={5} cleanup-failed={6} phase={7}");
    private readonly TaskCompletionSource requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource fulfilled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long started = Stopwatch.GetTimestamp();
    private int requests;
    private int loads;
    private ProbePhase phase = ProbePhase.Pending;
    private bool safe;
    private bool positiveReleased;
    private bool cleanupFailed;

    /// <summary>Rejects an incomplete document, then requires completed delivery and two safe observations inside one budget.</summary>
    internal static async Task RunAsync(IPage page, string seed, Func<Task> beforePositive, Action afterPositive)
    {
        var probe = new HistoryReadinessProbe();
        await probe.RunCoreAsync(page, seed, beforePositive, afterPositive);
    }

    /// <summary>Owns route and load subscriptions, releases pending work on every exit and preserves the primary failure.</summary>
    private async Task RunCoreAsync(IPage page, string seed, Func<Task> beforePositive, Action afterPositive)
    {
        EventHandler<IPage> loaded = (_, _) => Interlocked.Increment(ref loads);
        page.Load += loaded;
        try
        {
            await page.RouteAsync(RoutePattern, RespondAsync);
            await RequirePendingAsync(page, seed);
            phase = ProbePhase.GraphStartup;
            await beforePositive();
            var budget = new HistoryObservationBudget();
            phase = ProbePhase.Delivery;
            positiveReleased = true;
            release.TrySetResult();
            await budget.ObserveAsync(fulfilled.Task, timeout => InspectAsync(page, seed, timeout), TestContext.Current.CancellationToken);
            await budget.ObserveAsync(fulfilled.Task, timeout => InspectAsync(page, seed, timeout), TestContext.Current.CancellationToken);
            safe = true;
            phase = ProbePhase.Complete;
            afterPositive();
        }
        finally
        {
            release.TrySetResult();
            try
            {
                await page.UnrouteAllAsync(new()
                {
                    Behavior = UnrouteBehavior.Wait
                }).WaitAsync(CleanupDeadline);
            }
            catch (Exception) { cleanupFailed = true; }
            finally { page.Load -= loaded; WriteEvidence(); }
        }
        Assert.False(cleanupFailed, CleanupFailure);
    }

    /// <summary>Marks successful delivery before entering the atomic browser predicate.</summary>
    private Task InspectAsync(IPage page, string seed, float timeout)
    {
        phase = ProbePhase.Inspection;
        return AdministratorHistory.RequireNoRedisplayAsync(page, seed, timeout);
    }

    /// <summary>Proves the held image prevents completion and that the real oracle rejects the incomplete page.</summary>
    private async Task RequirePendingAsync(IPage page, string seed)
    {
        await page.GotoAsync(Origin, new()
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        await requested.Task.WaitAsync(HistoryObservationBudget.Limit, TestContext.Current.CancellationToken);
        Assert.True(await page.EvaluateAsync<string>(Readiness) != Complete);
        await Assert.ThrowsAsync<TimeoutException>(() => AdministratorHistory.RequireNoRedisplayAsync(page, seed, IncompleteTimeoutMilliseconds));
    }

    /// <summary>Separates release permission from successful fulfillment and forwards route failures to the awaiting probe.</summary>
    private async Task RespondAsync(IRoute route)
    {
        try
        {
            if (new Uri(route.Request.Url).AbsolutePath == Pending)
            {
                Interlocked.Increment(ref requests);
                requested.TrySetResult();
                await release.Task;
                await route.FulfillAsync(new()
                {
                    ContentType = SvgContentType,
                    Body = Image
                });
                fulfilled.TrySetResult();
            }
            else
            {
                await route.FulfillAsync(new()
                {
                    ContentType = HtmlContentType,
                    Body = HeldDocument
                });
            }
        }
        catch (Exception error)
        {
            requested.TrySetException(error);
            fulfilled.TrySetException(error);
        }
    }

    /// <summary>Emits only counters, fixed booleans and relative time; never page markup, request URLs or exception text.</summary>
    private void WriteEvidence()
    {
        var line = string.Format(CultureInfo.InvariantCulture, Evidence, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Volatile.Read(ref requests), positiveReleased, fulfilled.Task.IsCompletedSuccessfully, Volatile.Read(ref loads), safe, cleanupFailed, phase);
        try
        {
            TestContext.Current.TestOutputHelper!.WriteLine(line);
        }
        catch (Exception) { /* Output failures cannot mask the original browser result. */ }
    }
    private enum ProbePhase
    {
        Pending, GraphStartup, Delivery, Inspection, Complete
    }
}
