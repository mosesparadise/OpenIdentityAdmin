using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Identifies one read or explicit transaction epoch without allowing stale deferred work to adopt a successor.</summary>
internal sealed class ProtocolStoragePhase(ProtocolStorageLifetime lifetime)
{
    internal ProtocolStorageLifetime Lifetime { get; } = lifetime;
    internal IDbContextTransaction? Transaction
    {
        get; set;
    }
    internal bool Closing
    {
        get; set;
    }
    internal bool Committed
    {
        get; set;
    }

    /// <summary>Checks exact native transaction ownership and rejects writes outside an explicit transaction.</summary>
    internal void RequireCurrent(DbContext database, bool write)
    {
        Lifetime.RequireCurrent();
        if (Closing || Committed || !ReferenceEquals(database.Database.CurrentTransaction, Transaction) ||
            (write && Transaction is null))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
