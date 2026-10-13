using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Bounds sanitized observation history and preserves each role's first observed unavailable state independently.</summary>
internal sealed class ProfileTransitionLog
{
    internal const int HistoryLimit = 128;
    private static readonly CompositeFormat EntryFormat = CompositeFormat.Parse("sequence={0} elapsed-ms={1} phase={2} role={3} {4} health={5}");
    private static readonly CompositeFormat HeaderFormat = CompositeFormat.Parse("profile-transitions observed={0} retained={1} truncated={2} observer-failed={3} joined={4} publication-drain=not-confirmed");
    private static readonly CompositeFormat FirstFormat = CompositeFormat.Parse("profile-first-unavailable role={0} {1}");
    private static readonly CompositeFormat LastFormat = CompositeFormat.Parse("profile-last-observed role={0} {1}");
    private const string Missing = "not-observed";
    private const string Unknown = "unknown";
    private readonly object gate = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<string> history = [];
    private readonly Dictionary<string, string> signatures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> last = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> firstUnavailable = new(StringComparer.Ordinal);
    private long sequence;
    private bool failed;
    private bool joined;
    private ProfilePhase phase = ProfilePhase.Startup;

    /// <summary>Reports incomplete observer operation without exposing its exception or mutable history.</summary>
    internal bool ObserverFailed
    {
        /// <summary>Reads the observer-failure marker under the collector lock.</summary>
        get
        {
            lock (gate)
            {
                return failed;
            }
        }
    }

    /// <summary>Changes only the fixed test-phase label; invalid enum values are redacted when rendered.</summary>
    internal void SetPhase(ProfilePhase value)
    {
        lock (gate)
        {
            phase = value;
        }
    }

    /// <summary>Confirms cancellation callbacks and observer enumeration have both completed.</summary>
    internal void MarkJoined()
    {
        lock (gate)
        {
            joined = true;
        }
    }

    /// <summary>Marks observer failure without retaining or formatting the underlying exception.</summary>
    internal void MarkFailed()
    {
        lock (gate)
        {
            failed = true;
        }
    }

    /// <summary>Retains only known roles and allowlisted scalar fields, deduplicating equal sanitized observations.</summary>
    internal void Record(string role, CustomResourceSnapshot snapshot)
    {
        if (!ProfileStartupEvidence.ResourceNames.Contains(role, StringComparer.Ordinal))
        {
            return;
        }
        lock (gate)
        {
            var safe = ProfileStartupEvidence.Describe(snapshot);
            var health = DescribeHealth(snapshot.HealthStatus);
            var signature = safe + health + DescribePhase(phase);
            if (signatures.TryGetValue(role, out var prior) && prior == signature)
            {
                return;
            }
            signatures[role] = signature;
            var entry = string.Format(CultureInfo.InvariantCulture, EntryFormat, ++sequence,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, DescribePhase(phase), role, safe, health);
            last[role] = entry;
            if (snapshot.HealthStatus != HealthStatus.Healthy && IsUnavailable(snapshot.State?.Text))
            {
                firstUnavailable.TryAdd(role, entry);
            }
            if (history.Count < HistoryLimit)
            {
                history.Add(entry);
            }
        }
    }

    /// <summary>Returns a bounded immutable copy with explicit truncation, missing-role and observer-failure evidence.</summary>
    internal string[] Lines()
    {
        lock (gate)
        {
            var lines = new List<string>
            {
                string.Format(CultureInfo.InvariantCulture, HeaderFormat, sequence, history.Count, sequence > history.Count, failed, joined)
            };
            lines.AddRange(history);
            foreach (var role in ProfileStartupEvidence.ResourceNames)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture, FirstFormat, role, firstUnavailable.GetValueOrDefault(role, Missing)));
                lines.Add(string.Format(CultureInfo.InvariantCulture, LastFormat, role, last.GetValueOrDefault(role, Missing)));
            }
            return lines.ToArray();
        }
    }

    /// <summary>Matches pinned Aspire unavailable process states; ordinary unhealthy readiness is not terminal.</summary>
    private static bool IsUnavailable(string? state) => state == KnownResourceStates.Finished || state == KnownResourceStates.Exited
        || state == KnownResourceStates.FailedToStart || state == KnownResourceStates.RuntimeUnhealthy;

    /// <summary>Maps only the framework health enum; custom descriptions and exceptions never enter evidence.</summary>
    private static string DescribeHealth(HealthStatus? health) => health switch
    {
        HealthStatus.Healthy => nameof(HealthStatus.Healthy),
        HealthStatus.Degraded => nameof(HealthStatus.Degraded),
        HealthStatus.Unhealthy => nameof(HealthStatus.Unhealthy),
        _ => Unknown
    };

    /// <summary>Maps only source-owned phases, including unknown enum values without arbitrary formatting.</summary>
    private static string DescribePhase(ProfilePhase value) => value switch
    {
        ProfilePhase.Startup => nameof(ProfilePhase.Startup),
        ProfilePhase.HostReadiness => nameof(ProfilePhase.HostReadiness),
        ProfilePhase.ClosedIssuer => nameof(ProfilePhase.ClosedIssuer),
        ProfilePhase.DatabaseStop => nameof(ProfilePhase.DatabaseStop),
        ProfilePhase.DegradedHealth => nameof(ProfilePhase.DegradedHealth),
        _ => Unknown
    };
}

/// <summary>Source-owned profile phases; no runtime/configuration strings become phase labels.</summary>
internal enum ProfilePhase
{
    Startup, HostReadiness, ClosedIssuer, DatabaseStop, DegradedHealth
}
