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
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        if (!LiveAtCurrentTime(row))
        {
            return false;
        }
        await AuditAsync(row.Digest, row.Revision, SessionMutation.Create, cancellationToken);
        if (!LiveAtCurrentTime(row))
        {
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Updates one live expected revision without upsert/retry, checking expiry again after lock and audit waits.</summary>
    internal async Task<bool> RenewAsync(AdminSessionTicket original, AdminSessionTicket replacement, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcTicks;
        var changed = await database.Set<AdminSessionTicket>().Where(row => row.Digest == original.Digest &&
            row.Revision == original.Revision && row.RevokedAt == null && row.CreatedAt <= now &&
            row.LastActivityAt <= now && row.IdleExpiresAt > now && row.AbsoluteExpiresAt > now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(row => row.LastActivityAt, replacement.LastActivityAt)
                .SetProperty(row => row.IdleExpiresAt, replacement.IdleExpiresAt)
                .SetProperty(row => row.Revision, replacement.Revision)
                .SetProperty(row => row.ProtectedTicket, replacement.ProtectedTicket), cancellationToken);
        if (changed != 1 || !LiveAtCurrentTime(original) || !LiveAtCurrentTime(replacement))
        {
            return false;
        }
        await AuditAsync(original.Digest, replacement.Revision, SessionMutation.Renew, cancellationToken);
        if (!LiveAtCurrentTime(original) || !LiveAtCurrentTime(replacement))
        {
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Revokes whichever revision currently exists; the write lock retains its revision until atomic intent commits.</summary>
    internal async Task RevokeAsync(string digest, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcTicks;
        var changed = await database.Set<AdminSessionTicket>().Where(row => row.Digest == digest && row.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.RevokedAt, now), cancellationToken);
        if (changed == 0)
        {
            return;
        }
        var revision = await database.Set<AdminSessionTicket>().Where(row => row.Digest == digest)
            .Select(row => row.Revision).SingleAsync(cancellationToken);
        await AuditAsync(digest, revision, SessionMutation.Revoke, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    /// <summary>Resamples time after asynchronous waits so stale pre-lock time cannot revive expired tickets.</summary>
    private bool LiveAtCurrentTime(AdminSessionTicket row)
    {
        var now = clock.GetUtcNow().UtcTicks;
        return row.RevokedAt is null && now >= row.CreatedAt && now >= row.LastActivityAt &&
            now < row.IdleExpiresAt && now < row.AbsoluteExpiresAt;
    }

    /// <summary>Appends minimal intent inside the caller transaction; failures escape so transaction disposal rolls back state.</summary>
    private async Task AuditAsync(string digest, long revision, SessionMutation operation, CancellationToken cancellationToken)
    {
        database.Add(new AdminSessionAudit
        {
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = clock.GetUtcNow().UtcTicks,
            Digest = digest,
            Revision = revision,
            Operation = operation,
            Outcome = SessionMutationOutcome.Committed
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
