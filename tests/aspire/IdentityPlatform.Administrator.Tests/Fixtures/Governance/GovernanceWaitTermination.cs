using System.Diagnostics;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Requires real native wait evidence before cancellation and always joins the subject and diagnostic probe.</summary>
internal static class GovernanceWaitTermination
{
    private const int OperationSeconds = 5;
    private const int DrainSeconds = 10;
    private const string MissingWait = "The owned operation ended before its native wait was observed.";
    private const string Incomplete = "The owned operation or wait probe failed to finish during bounded cleanup.";

    /// <summary>Distinguishes deadline and caller cancellation using native observations, never a sanitized exception alone.</summary>
    internal static async Task RequireAsync(Task subject, Task wait, CancellationTokenSource caller,
        CancellationTokenSource probe, GovernanceCancellationEvidence evidence, bool cancel, bool service, Stopwatch? watch = null, Func<ValueTask>? failedCleanupRelease = null)
    {
        try
        {
            Assert.True(await Task.WhenAny(wait, subject) == wait, MissingWait);
            await wait;
            Assert.False(subject.IsCompleted || caller.IsCancellationRequested || evidence.ObservedToken.IsCancellationRequested);
            Assert.True(evidence.Commands > 0 && evidence.TokenMatched);
            if (cancel)
            {
                Assert.True(watch is null || watch.Elapsed < TimeSpan.FromSeconds(OperationSeconds));
                await caller.CancelAsync();
            }
            var failure = await Record.ExceptionAsync(() => subject.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
            Assert.True(subject.IsCompleted && failure is not null);
            Assert.True(cancel ? evidence.NativeCallerCancellation : evidence.NativeDeadlineCancellation);
            if (service)
            {
                Assert.True(failure is OperationCanceledException or GovernanceUnavailableException);
            }
        }
        finally
        {
            await CleanupAsync(subject, wait, caller, probe, failedCleanupRelease);
        }
    }

    /// <summary>Releases a failed test's native blocker only for cleanup, then joins even if cancellation or disposal callbacks fail.</summary>
    private static async Task CleanupAsync(Task subject, Task wait, CancellationTokenSource caller,
        CancellationTokenSource probe, Func<ValueTask>? release)
    {
        try
        {
            await caller.CancelAsync();
        }
        finally
        {
            try
            {
                await probe.CancelAsync();
            }
            finally
            {
                try
                {
                    if (!subject.IsCompleted && release is not null)
                    {
                        await release();
                    }
                }
                finally { await DrainAsync(subject, wait); }
            }
        }
    }

    /// <summary>Always releases a failed subject's blocker when needed and joins its work before the context can be disposed.</summary>
    internal static async Task CleanupSubjectAsync(Task subject, CancellationTokenSource caller, Func<ValueTask> release)
    {
        try
        {
            await caller.CancelAsync();
        }
        finally
        {
            try
            {
                if (!subject.IsCompleted)
                {
                    await release();
                }
            }
            finally { await DrainAsync(subject); }
        }
    }

    /// <summary>Joins faults and cancellation without reporting them as successful test evidence or retaining diagnostics.</summary>
    private static async Task DrainAsync(params Task[] tasks)
    {
        var complete = Task.WhenAll(tasks);
        _ = await Record.ExceptionAsync(() => complete.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
        Assert.True(complete.IsCompleted, Incomplete);
    }
}
