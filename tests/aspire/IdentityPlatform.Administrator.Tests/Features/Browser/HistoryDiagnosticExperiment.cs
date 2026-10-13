using System.Diagnostics;
using System.Globalization;
using System.Text;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Runs explicitly selected fixed diagnostics separately from ordinary browser acceptance.</summary>
internal static class HistoryDiagnosticExperiment
{
    internal const string ModeVariable = "IDENTITY_HISTORY_EXPERIMENT";
    internal const string Isolated = "isolated";
    internal const string Overlap = "overlap";
    private const string Ordinary = "ordinary";
    private const string Firefox = "firefox";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string InvalidMode = "Unknown history diagnostic mode.";
    private static readonly CompositeFormat Complete = CompositeFormat.Parse("HISTORY_EXPERIMENT mode={0} provider={1} completed=True overlap={2}");

    /// <summary>Rejects unsupported explicit modes; an absent variable retains the ordinary test's behavior.</summary>
    internal static string SelectMode(string? mode) => mode switch
    {
        null => Ordinary,
        Isolated => Isolated,
        Overlap => Overlap,
        _ => throw new InvalidOperationException(InvalidMode)
    };

    /// <summary>Requires Firefox and an exact provider for focused experiments; ordinary acceptance starts no extra graph.</summary>
    internal static async Task RunAsync(string provider, Func<Func<Task>, Action, Task> observation)
    {
        var mode = SelectMode(Environment.GetEnvironmentVariable(ModeVariable));
        if (mode == Ordinary)
        {
            await observation(() => Task.CompletedTask, () => { });
            return;
        }
        Assert.Equal(Firefox, BrowserSession.Engine);
        Assert.True(provider is PostgreSql or SqlServer);
        var window = new HistoryDiagnosticWindow();
        try
        {
            if (mode == Isolated)
            {
                await observation(window.BeginAsync, window.End);
            }
            else
            {
                await HistoryStartupGraph.ObserveAsync(provider, observation, window);
            }
            window.Require(mode == Overlap);
            WriteSafely(string.Format(CultureInfo.InvariantCulture, Complete, mode, provider, mode == Overlap));
        }
        finally
        {
            WriteSafely(window.Describe());
        }
    }

    /// <summary>Retains fixed evidence on failures without replacing the primary diagnostic exception.</summary>
    internal static void WriteSafely(string message)
    {
        try
        {
            TestContext.Current.TestOutputHelper!.WriteLine(message);
        }
        catch (Exception) { /* Evidence output cannot replace the primary test result. */ }
    }

}

/// <summary>Retains only relative monotonic timestamps and proves actual positive-window/startup interval intersection.</summary>
internal sealed class HistoryDiagnosticWindow
{
    private static readonly CompositeFormat Format = CompositeFormat.Parse(
        "HISTORY_WINDOW unit=microseconds graph-start={0} database-running={1} graph-ready={2} positive-start={3} positive-end={4}");
    private const long Missing = -1;
    private readonly long origin = Stopwatch.GetTimestamp();
    private long graphStart = Missing;
    private long databaseRunning = Missing;
    private long graphReady = Missing;
    private long positiveStart = Missing;
    private long positiveEnd = Missing;

    /// <summary>Marks the start of the actual graph operation.</summary>
    internal void GraphStarted() => graphStart = Now();
    /// <summary>Marks observed database Running before the positive browser window.</summary>
    internal void DatabaseRunning() => databaseRunning = Now();
    /// <summary>Marks completion only after all configured host readiness waits pass.</summary>
    internal void GraphReady() => graphReady = Now();
    /// <summary>Starts the positive observation window immediately before its own five-second budget begins.</summary>
    internal Task BeginAsync()
    {
        positiveStart = Now();
        return Task.CompletedTask;
    }
    /// <summary>Ends the positive window before route teardown or remaining graph readiness work.</summary>
    internal void End() => positiveEnd = Now();
    /// <summary>Returns monotonic time relative to this synthetic experiment, without wall-clock or process metadata.</summary>
    private long Now() => (long)Stopwatch.GetElapsedTime(origin).TotalMicroseconds;
    /// <summary>Rejects absent/empty windows and nonoverlapping startup; an inconclusive arm is never silently rerun.</summary>
    internal void Require(bool overlap)
    {
        Assert.True(positiveStart >= 0 && positiveEnd > positiveStart);
        Assert.True(positiveEnd - positiveStart <= (long)HistoryObservationBudget.Limit.TotalMicroseconds);
        if (overlap)
        {
            Assert.True(graphStart >= 0 && databaseRunning >= graphStart && positiveStart >= databaseRunning && graphReady > positiveStart);
        }
        else
        {
            Assert.Equal(Missing, graphStart);
            Assert.Equal(Missing, databaseRunning);
            Assert.Equal(Missing, graphReady);
        }
    }
    /// <summary>Formats the fixed numeric evidence consumed by the separate experiment verifier.</summary>
    internal string Describe() => string.Format(CultureInfo.InvariantCulture, Format, graphStart, databaseRunning, graphReady, positiveStart, positiveEnd);
}
