using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Bounds a diagnostic read and owns unfinished work through page close and a separate cleanup budget.</summary>
internal static class HistoryDocumentRead
{
    private static readonly TimeSpan CleanupBudget = TimeSpan.FromSeconds(5);

    /// <summary>Never launches expired work; cancellation closes the page and drains its pending read before returning.</summary>
    internal static async Task<T> RunAsync<T>(Func<Task<T>> read, TimeSpan budget, Func<Task> close,
        CancellationToken cancellationToken, TimeProvider? clock = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (budget <= TimeSpan.Zero)
        {
            throw new TimeoutException();
        }
        clock ??= TimeProvider.System;
        var started = clock.GetTimestamp();
        var pending = read();
        try
        {
            var remaining = budget - clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException();
            }
            var result = await pending.WaitAsync(remaining, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (clock.GetElapsedTime(started) >= budget)
            {
                throw new TimeoutException();
            }
            return result;
        }
        catch when (!pending.IsCompleted)
        {
            try
            {
                await Task.WhenAll(ObserveAsync(pending), close()).WaitAsync(CleanupBudget, CancellationToken.None);
            }
            catch (Exception)
            {
                throw new HistoryDocumentCleanupException();
            }
            throw;
        }
    }

    /// <summary>Consumes the read's cleanup fault without replacing its previously recorded timeout or cancellation.</summary>
    private static async Task ObserveAsync(Task pending)
    {
        try
        {
            await pending;
        }
        catch (Exception) { /* The original bounded read failure remains primary. */ }
    }
}

/// <summary>Stops further controls when owned page teardown cannot establish read completion.</summary>
internal sealed class HistoryDocumentCleanupException : PlaywrightException
{
    private const string Failure = "Diagnostic document read cleanup remained incomplete.";

    /// <summary>Uses a fixed safe category without retaining browser diagnostic data.</summary>
    internal HistoryDocumentCleanupException() : base(Failure) { }
}
