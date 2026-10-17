using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Serializes native consumption and quota decisions with all supported mutations in their owning database.</summary>
internal static class AdministratorProtocolLocks
{
    /// <summary>Acquires the profile boundary after governance and before account locks; a missing profile fails closed.</summary>
    internal static ValueTask AcquireProfileAsync(ProtocolStorageOwner owner, CancellationToken token) =>
        owner.RunAsync(async cancellation =>
        {
            var database = RequireFresh<PlatformDbContext>(owner);
            await ConfigureTimeoutAsync(database, cancellation);
            RequireSingle(await database.Set<AdministratorProtocolProfile>()
                .Where(row => row.Id == AdministratorProtocolProfile.SingletonId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.LockRevision, row => row.LockRevision + 1), cancellation));
        }, token, write: true);

    /// <summary>Acquires isolated native client-state ownership before reading any state row or sending remote HTTP.</summary>
    internal static ValueTask AcquireClientStateAsync(ProtocolStorageOwner owner, CancellationToken token) =>
        owner.RunAsync(async cancellation =>
        {
            var database = RequireFresh<AdminSessionDbContext>(owner);
            await ConfigureTimeoutAsync(database, cancellation);
            RequireSingle(await database.Set<AdministratorClientStateBudget>()
                .Where(row => row.Id == AdministratorClientStateBudget.SingletonId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), cancellation));
        }, token, write: true);

    /// <summary>Rejects the wrong database or a preloaded tracked view rather than promoting stale native entities to authority.</summary>
    private static T RequireFresh<T>(ProtocolStorageOwner owner) where T : DbContext
    {
        if (owner.Database is not T database || database.ChangeTracker.Entries().Any())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return database;
    }

    /// <summary>Uses the installed provider's transaction-local lock budget without selecting providers in shared code.</summary>
    private static async Task ConfigureTimeoutAsync(DbContext database, CancellationToken token)
    {
        if (database.GetDatabaseProvider().Connections.TransactionLockLimitSql is { } command)
        {
            await database.Database.ExecuteSqlRawAsync(command, token);
        }
    }

    /// <summary>Never recreates coordination state from a live protocol request.</summary>
    private static void RequireSingle(int affected)
    {
        if (affected != 1)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
