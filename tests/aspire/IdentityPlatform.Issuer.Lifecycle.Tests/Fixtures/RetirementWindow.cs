using IdentityPlatform.Testing;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

// Evidence for this controlled two-node experiment only; not a production cluster ledger.
internal sealed class RetirementWindow(TimeProvider? timeProvider = null)
{
    private readonly Dictionary<string, HashSet<ProcessGeneration>> old = new(StringComparer.Ordinal);
    private Dictionary<string, ProcessGeneration>? replacements;
    private DateTimeOffset? ended;
    private long? endedTimestamp;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public void ObserveOld(string node, ProcessGeneration generation)
    {
        Assert.Contains(node, DistributionEvidence.Nodes);
        if (!old.TryGetValue(node, out var generations))
        {
            old.Add(node, generations = []);
        }
        generations.Add(generation);
        ended = null;
        endedTimestamp = null;
        replacements = null;
    }

    public bool ConfirmEnded(IReadOnlyDictionary<string, ProcessGeneration> current, DateTimeOffset now,
        Func<ProcessGeneration, bool> terminated)
    {
        ended = null;
        endedTimestamp = null;
        replacements = null;
        if (old.Count != 2 || current.Count != 2 || DistributionEvidence.Nodes.Any(node =>
            !current.TryGetValue(node, out var generation) || !old.TryGetValue(node, out var prior) || prior.Contains(generation)) ||
            old.Values.SelectMany(value => value).Any(generation => !terminated(generation)))
        {
            return false;
        }
        ended = now; // Conservative boundary captured after every known old-active generation actually stopped.
        endedTimestamp = clock.GetTimestamp();
        replacements = new Dictionary<string, ProcessGeneration>(current, StringComparer.Ordinal);
        return true;
    }

    public DateTimeOffset Earliest => (ended ?? throw new InvalidOperationException("Old issuance has no complete termination evidence.")).AddSeconds(300);

    public bool CanRetire(IReadOnlyDictionary<string, ProcessGeneration> current, DateTimeOffset now, params DateTimeOffset[] originalExpirations) =>
        ended is not null && endedTimestamp is { } timestamp && clock.GetElapsedTime(timestamp) > TimeSpan.FromSeconds(300) && replacements is not null && current.Count == 2 &&
        replacements.All(pair => current.TryGetValue(pair.Key, out var generation) && generation == pair.Value) &&
        now > Earliest && originalExpirations.All(expiration => now > expiration);
}
