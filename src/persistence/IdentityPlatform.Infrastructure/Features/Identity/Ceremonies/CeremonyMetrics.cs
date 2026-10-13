using System.Diagnostics.Metrics;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

internal enum CeremonyOutcome
{
    Verified, ReplayDenied, CapacityDenied, Locked
}

/// <summary>Emits only fixed low-cardinality outcomes and metadata counts; identifiers and credentials are never metric tags.</summary>
internal static class CeremonyMetrics
{
    internal const string MeterName = "IdentityPlatform.Administrator.Ceremonies";
    private const string DecisionsName = "administrator.ceremony.decisions";
    private const string BacklogName = "administrator.cleanup.backlog";
    private const string OutcomeTag = "outcome";
    private const string CategoryTag = "category";
    private const string Verified = "verified";
    private const string ReplayDenied = "replay-denied";
    private const string CapacityDenied = "capacity-denied";
    private const string Locked = "locked";
    internal const string Interactions = "interactions";
    internal const string Sessions = "sessions";
    internal const string Browsers = "browsers";
    internal const string Replays = "replays";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(DecisionsName);
    private static readonly Histogram<long> Backlog = Meter.CreateHistogram<long>(BacklogName);

    /// <summary>Counts a fixed mechanism outcome; verification does not claim that its surrounding transaction committed.</summary>
    internal static void Record(CeremonyOutcome outcome) => Decisions.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome switch
    {
        CeremonyOutcome.Verified => Verified,
        CeremonyOutcome.ReplayDenied => ReplayDenied,
        CeremonyOutcome.CapacityDenied => CapacityDenied,
        CeremonyOutcome.Locked => Locked,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    }));

    /// <summary>Records only the four code-owned cleanup categories and their measured eligible row counts.</summary>
    internal static void RecordBacklog(string category, long count)
    {
        if (category is not (Interactions or Sessions or Browsers or Replays))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }
        Backlog.Record(count, new KeyValuePair<string, object?>(CategoryTag, category));
    }
}
