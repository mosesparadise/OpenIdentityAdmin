using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Observes Aspire transitions for a test's lifetime without allowing diagnostic failures to replace test failures.</summary>
internal static class ProfileTransitionObserver
{
    private static readonly TimeSpan JoinDeadline = TimeSpan.FromSeconds(1);
    /// <summary>Starts observation before startup, attempts a bounded join before disposal and reports primary or observer failures.</summary>
    internal static Task ObserveAsync(DistributedApplication app, Func<ProfileTransitionLog, Task> action) =>
        ObserveAsync(app.ResourceNotifications.WatchAsync, action, message => TestContext.Current.TestOutputHelper!.WriteLine(message));

    /// <summary>Owns the watcher token independently of the case deadline; cancellation never changes the primary result.</summary>
    internal static async Task ObserveAsync(Func<CancellationToken, IAsyncEnumerable<ResourceEvent>> watch,
        Func<ProfileTransitionLog, Task> action, Action<string> write)
    {
        var log = new ProfileTransitionLog();
        var lifetime = new CancellationTokenSource();
        var observing = PumpAsync(watch, log, lifetime.Token);
        var failed = false;
        try
        {
            await action(log);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            await StopAsync(lifetime, observing, log);
            if (failed || log.ObserverFailed)
            {
                WriteSafely(log, write);
            }
        }
    }

    /// <summary>Consumes the pinned watch stream synchronously through its first suspension, before the tested action starts.</summary>
    private static async Task PumpAsync(Func<CancellationToken, IAsyncEnumerable<ResourceEvent>> watch,
        ProfileTransitionLog log, CancellationToken token)
    {
        try
        {
            await foreach (var change in watch(token).ConfigureAwait(false))
            {
                try
                {
                    log.Record(change.Resource.Name, change.Snapshot);
                }
                catch (Exception) { log.MarkFailed(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { log.MarkFailed(); }
    }

    /// <summary>Bounds observer joining separately from application deadlines and never masks a primary exception.</summary>
    private static async Task StopAsync(CancellationTokenSource lifetime, Task observing, ProfileTransitionLog log)
    {
        try
        {
            await CompleteStopAsync(lifetime, observing, log).WaitAsync(JoinDeadline);
        }
        catch (Exception) { log.MarkFailed(); }
    }

    /// <summary>Retains token ownership after a timed-out join until callbacks and the sole enumerator owner finish.</summary>
    private static async Task CompleteStopAsync(CancellationTokenSource lifetime, Task observing, ProfileTransitionLog log)
    {
        try
        {
            await Task.WhenAll(lifetime.CancelAsync(), observing);
        }
        catch (Exception) { log.MarkFailed(); }
        finally
        {
            log.MarkJoined();
            lifetime.Dispose();
        }
    }

    /// <summary>Attempts every bounded line even if an output sink fails, without serializing the sink exception.</summary>
    private static void WriteSafely(ProfileTransitionLog log, Action<string> write)
    {
        foreach (var line in log.Lines())
        {
            try
            {
                write(line);
            }
            catch (Exception) { /* Evidence output cannot replace the primary test failure. */ }
        }
    }
}
