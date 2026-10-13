namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Shares one absolute five-second budget across delivery completion and browser inspection.</summary>
internal sealed class HistoryObservationBudget
{
    internal static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private const string Expired = "History observation budget expired before a safe completed document was observed.";
    private readonly TimeProvider clock;
    private readonly long started;

    /// <summary>Starts the absolute window before release; a clock seam supports deterministic expiry controls.</summary>
    internal HistoryObservationBudget(TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        started = this.clock.GetTimestamp();
    }

    /// <summary>Rejects exhausted budgets instead of passing zero, which disables Playwright timeouts.</summary>
    internal TimeSpan Remaining()
    {
        var remaining = Limit - clock.GetElapsedTime(started);
        return remaining > TimeSpan.Zero ? remaining : throw new TimeoutException(Expired);
    }

    /// <summary>Propagates fulfillment failure and gives inspection only the time left in the same absolute window.</summary>
    internal async Task ObserveAsync(Task fulfillment, Func<float, Task> inspect, CancellationToken token)
    {
        await fulfillment.WaitAsync(Remaining(), token);
        await inspect((float)Remaining().TotalMilliseconds).WaitAsync(Remaining(), token);
        _ = Remaining();
    }
}
