using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Stages ticket mutations in an existing Sessions transaction and guards its single commit; the caller owns rollback and disposal.</summary>
internal sealed class SessionTicketTransaction
{
    private const string InvalidOwner = "Session transaction ownership is unavailable.";
    private const int Ready = 0;
    private const int Staging = 1;
    private const int Closed = 2;
    private readonly AdminSessionDbContext database;
    private readonly IDbContextTransaction transaction;
    private readonly TimeProvider clock;
    private readonly List<SessionTicketDeadline> deadlines = [];
    private int state;

    /// <summary>Binds the exact context and current transaction without opening a scope, beginning a transaction or taking disposal ownership.</summary>
    internal SessionTicketTransaction(AdminSessionDbContext database, IDbContextTransaction transaction, TimeProvider clock)
    {
        this.database = database;
        this.transaction = transaction;
        this.clock = clock;
        EnsureCurrent();
    }

    /// <summary>Stages protected state and audit together; false or an exception makes this participant permanently uncommittable.</summary>
    internal Task<bool> CreateAsync(AdminSessionTicket row, CancellationToken cancellationToken) => StageAsync(async () =>
    {
        deadlines.Add(SessionTicketDeadline.Capture(row));
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        if (!Live())
        {
            return false;
        }
        await AuditAsync(row.Digest, row.Revision, SessionMutation.Create, cancellationToken);
        return Live();
    });

    /// <summary>Stages one live expected revision without upsert or retry, retaining immutable deadlines across later owner work.</summary>
    internal Task<bool> RenewAsync(AdminSessionTicket original, AdminSessionTicket replacement, CancellationToken cancellationToken) => StageAsync(async () =>
    {
        deadlines.Add(SessionTicketDeadline.Capture(original));
        deadlines.Add(SessionTicketDeadline.Capture(replacement));
        var now = clock.GetUtcNow().UtcTicks;
        var changed = await database.Set<AdminSessionTicket>().Where(row => row.Digest == original.Digest &&
            row.Revision == original.Revision && row.RevokedAt == null && row.CreatedAt <= now &&
            row.LastActivityAt <= now && row.IdleExpiresAt > now && row.AbsoluteExpiresAt > now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(row => row.LastActivityAt, replacement.LastActivityAt)
                .SetProperty(row => row.IdleExpiresAt, replacement.IdleExpiresAt)
                .SetProperty(row => row.Revision, replacement.Revision)
                .SetProperty(row => row.ProtectedTicket, replacement.ProtectedTicket), cancellationToken);
        if (changed != 1 || !Live())
        {
            return false;
        }
        await AuditAsync(original.Digest, replacement.Revision, SessionMutation.Renew, cancellationToken);
        return Live();
    });

    /// <summary>Stages current-revision revocation; an absent or already revoked row permits the owner's other changes to commit.</summary>
    internal Task<bool> RevokeAsync(string digest, CancellationToken cancellationToken) => StageAsync(async () =>
    {
        var now = clock.GetUtcNow().UtcTicks;
        var changed = await database.Set<AdminSessionTicket>().Where(row => row.Digest == digest && row.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.RevokedAt, now), cancellationToken);
        if (changed == 0)
        {
            return false;
        }
        var revision = await database.Set<AdminSessionTicket>().Where(row => row.Digest == digest)
            .Select(row => row.Revision).SingleAsync(cancellationToken);
        await AuditAsync(digest, revision, SessionMutation.Revoke, cancellationToken);
        return true;
    }, allowUnchanged: true);

    /// <summary>Rechecks all original deadlines after owner work and attempts native commit once; uncertainty, cancellation and failure never permit retry.</summary>
    internal async Task<bool> CommitAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref state, Closed, Ready) != Ready)
        {
            Interlocked.Exchange(ref state, Closed);
            return false;
        }
        EnsureCurrent();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Live())
        {
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Marks work unsafe before the first await so caught errors, failed predicates and overlapping calls cannot authorize commit.</summary>
    private async Task<bool> StageAsync(Func<Task<bool>> mutation, bool allowUnchanged = false)
    {
        if (Interlocked.CompareExchange(ref state, Staging, Ready) != Ready)
        {
            Interlocked.Exchange(ref state, Closed);
            throw new InvalidOperationException(InvalidOwner);
        }
        try
        {
            EnsureCurrent();
            var changed = await mutation();
            EnsureCurrent();
            if (!changed && !allowUnchanged)
            {
                Interlocked.Exchange(ref state, Closed);
                return false;
            }
            return Interlocked.CompareExchange(ref state, Ready, Staging) == Staging && changed;
        }
        catch
        {
            Interlocked.Exchange(ref state, Closed);
            throw;
        }
    }

    /// <summary>Requires reference identity with the native transaction still attached to the originally supplied context.</summary>
    private void EnsureCurrent()
    {
        if (!ReferenceEquals(database.Database.CurrentTransaction, transaction))
        {
            throw new InvalidOperationException(InvalidOwner);
        }
    }

    /// <summary>Samples full-precision time against immutable snapshots, including backward-clock and expiry-equality denial.</summary>
    private bool Live()
    {
        var now = clock.GetUtcNow().UtcTicks;
        return deadlines.All(deadline => deadline.Live(now));
    }

    /// <summary>Appends B1 mutation intent in the shared transaction; this record does not claim authenticated B3 actor provenance.</summary>
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

/// <summary>Retains original lifetime evidence independently of mutable tracked or caller-owned rows.</summary>
internal readonly record struct SessionTicketDeadline(long Created, long Activity, long Idle, long Absolute, bool Revoked)
{
    /// <summary>Captures lifetime bounds before the first database wait.</summary>
    internal static SessionTicketDeadline Capture(AdminSessionTicket row) =>
        new(row.CreatedAt, row.LastActivityAt, row.IdleExpiresAt, row.AbsoluteExpiresAt, row.RevokedAt is not null);

    /// <summary>Rejects revocation, rollback and equality with either original expiry.</summary>
    internal bool Live(long now) => !Revoked && now >= Created && now >= Activity && now < Idle && now < Absolute;
}
