using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Owns bounded database mutations and their audit transactions; callers must use a fresh scoped context per operation.</summary>
internal sealed class SessionTicketPersistence(AdminSessionDbContext database, TimeProvider clock)
{
    /// <summary>Retrieves at most one detached row without activity updates.</summary>
    internal Task<AdminSessionTicket?> FindAsync(string digest, CancellationToken cancellationToken) =>
        database.Set<AdminSessionTicket>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == digest, cancellationToken);

    /// <summary>Commits protected state and intent together, returning false if database waits consumed its lifetime.</summary>
    internal async Task<bool> CreateAsync(AdminSessionTicket row, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var participant = new SessionTicketTransaction(database, transaction, clock);
        return await participant.CreateAsync(row, cancellationToken) && await participant.CommitAsync(cancellationToken);
    }

    /// <summary>Updates one live expected revision without upsert/retry inside an owned transaction.</summary>
    internal async Task<bool> RenewAsync(AdminSessionTicket original, AdminSessionTicket replacement, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var participant = new SessionTicketTransaction(database, transaction, clock);
        return await participant.RenewAsync(original, replacement, cancellationToken) && await participant.CommitAsync(cancellationToken);
    }

    /// <summary>Revokes the current revision and audit atomically; absent rows remain an idempotent no-op.</summary>
    internal async Task RevokeAsync(string digest, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var participant = new SessionTicketTransaction(database, transaction, clock);
        if (await participant.RevokeAsync(digest, cancellationToken))
        {
            await participant.CommitAsync(cancellationToken);
        }
    }

    /// <summary>Deletes at most the bounded candidate set, rechecking expiry/revocation after any concurrent renewal.</summary>
    internal async Task<int> CleanupAsync(DateTimeOffset capturedUtc, CancellationToken cancellationToken)
    {
        var now = capturedUtc.UtcTicks;
        var candidates = await Eligible(now).OrderBy(row => row.Digest).Select(row => row.Digest)
            .Take(SessionTicketLimits.CleanupBatch).ToArrayAsync(cancellationToken);
        return await Eligible(now).Where(row => candidates.Contains(row.Digest)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Builds the identical selection/deletion predicate without touching retained audit rows.</summary>
    private IQueryable<AdminSessionTicket> Eligible(long now) => database.Set<AdminSessionTicket>()
        .Where(row => row.RevokedAt != null || row.IdleExpiresAt <= now || row.AbsoluteExpiresAt <= now);

}
