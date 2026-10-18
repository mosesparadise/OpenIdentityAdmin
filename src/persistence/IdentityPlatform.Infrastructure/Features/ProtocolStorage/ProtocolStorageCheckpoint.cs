namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Freezes exact recognized write phases only after their confirmed database outcome and resource cleanup.</summary>
internal sealed class ProtocolStorageCheckpoint
{
    private readonly ProtocolStorageRequest request;
    private readonly Entry[] phases;

    /// <summary>Rejects unknown or incomplete outcomes, including commit attempts whose success was not confirmed.</summary>
    internal ProtocolStorageCheckpoint(ProtocolStorageRequest request, IReadOnlyList<ProtocolStoragePhase> values)
    {
        this.request = request;
        phases = values.Select(phase => new Entry(phase, phase.Transaction, phase.CommitAttempted, phase.Committed,
            phase.RolledBack, phase.CleanupCompleted)).ToArray();
        if (phases.Any(phase => !phase.IsSettled))
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Requires the same request, phase identities and immutable dispositions; formatting cannot add even a committed write phase.</summary>
    internal bool Matches(ProtocolStorageRequest current, IReadOnlyList<ProtocolStoragePhase> values) => ReferenceEquals(request, current) &&
        values.Count == phases.Length && phases.Select((entry, index) => entry.Matches(values[index])).All(match => match);

    /// <summary>Retains transaction identity and observed completion facts without retaining a scoped owner or provider.</summary>
    private sealed record Entry(ProtocolStoragePhase Phase, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? Transaction,
        bool CommitAttempted, bool Committed, bool RolledBack, bool CleanupCompleted)
    {
        internal bool IsSettled => Transaction is not null && CleanupCompleted &&
            (CommitAttempted && Committed && !RolledBack || !CommitAttempted && !Committed && RolledBack);

        /// <summary>Detects additions, substitutions or disposition changes even if the number of phases is unchanged.</summary>
        internal bool Matches(ProtocolStoragePhase phase) => ReferenceEquals(Phase, phase) && ReferenceEquals(Transaction, phase.Transaction) &&
            CommitAttempted == phase.CommitAttempted && Committed == phase.Committed && RolledBack == phase.RolledBack && CleanupCompleted == phase.CleanupCompleted;
    }
}
