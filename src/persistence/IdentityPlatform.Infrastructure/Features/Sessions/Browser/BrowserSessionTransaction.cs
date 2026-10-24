using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Owns bounded portable browser ordering and one guarded B1 commit; disposal precedes publication of any successful result.</summary>
internal sealed class BrowserSessionTransaction : IAsyncDisposable
{
    private const string Unavailable = "Browser session ownership is unavailable.";
    private readonly AdminSessionDbContext database;
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource deadline;
    private readonly CeremonyConnection connection;
    private readonly IDbContextTransaction transaction;
    private readonly List<BrowserDeadline> bounds = [];
    private readonly List<SessionTicketDeadline> actors = [];
    private bool attempted;
    private long cleanupObservation;
    internal SessionTicketTransaction Tickets
    {
        get;
    }
    internal CancellationToken Token => deadline.Token;
    internal long Now => clock.GetUtcNow().UtcTicks;

    /// <summary>Captures already acquired resources and binds the existing ticket participant to the exact native owner.</summary>
    private BrowserSessionTransaction(AdminSessionDbContext database, TimeProvider clock, CancellationTokenSource deadline,
        CeremonyConnection connection, IDbContextTransaction transaction)
    {
        this.database = database;
        this.clock = clock;
        this.deadline = deadline;
        this.connection = connection;
        this.transaction = transaction;
        Tickets = new(database, transaction, clock);
    }

    /// <summary>Acquires singleton ordering before reading quotas or browser state; missing seeds and dirty/reused contexts fail closed.</summary>
    internal static async Task<BrowserSessionTransaction> OpenAsync(AdminSessionDbContext database, TimeProvider clock, CancellationToken token)
    {
        if (database.Database.CurrentTransaction is not null || database.ChangeTracker.Entries().Any())
        {
            throw new InvalidOperationException(Unavailable);
        }
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        var connection = new CeremonyConnection(database);
        IDbContextTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(deadline.Token);
            transaction = await database.Database.BeginTransactionAsync(deadline.Token);
            var changed = await database.Set<BrowserSessionBudget>().Where(row => row.Id == 1 && row.Revision < long.MaxValue)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), deadline.Token);
            if (changed != 1)
            {
                throw new InvalidOperationException(Unavailable);
            }
            return new(database, clock, deadline, connection, transaction);
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
            finally { try { await connection.DisposeAsync(); } finally { deadline.Dispose(); } }
            throw;
        }
    }

    /// <summary>Denies cleanup if time moves backward after expired candidates were selected.</summary>
    internal void GuardCleanup(long observed) => cleanupObservation = observed;

    /// <summary>Captures immutable original operation bounds for final admission after all writes and audit waits.</summary>
    internal void Guard(long created, long observed, long expires) => bounds.Add(new(created, observed, expires));

    /// <summary>Records either explicitly anonymous provenance or a previously authenticated snapshot supplied by the owning operation.</summary>
    internal void Audit(BrowserContext browser, BrowserMutation operation, AdminSessionTicket? actor = null)
    {
        if (actor is not null)
        {
            actors.Add(SessionTicketDeadline.Capture(actor));
        }
        database.Add(new BrowserSessionAudit
        {
            Id = Guid.NewGuid(),
            CorrelationId = browser.CorrelationId,
            Generation = browser.Generation,
            OccurredAt = Now,
            Operation = operation,
            Subject = actor?.Subject,
            SourceSession = actor?.SourceSession
        });
    }

    /// <summary>Persists once, rechecks original browser deadlines, and delegates the sole native commit to the existing B1 guard.</summary>
    internal async Task<bool> CommitAsync()
    {
        if (attempted)
        {
            return false;
        }
        attempted = true;
        await database.SaveChangesAsync(Token);
        var now = Now;
        return now >= cleanupObservation && bounds.All(bound => bound.Current(now)) && actors.All(actor => actor.Live(now)) && await Tickets.CommitAsync(Token);
    }

    /// <summary>Rolls back uncommitted state, restores bounded connection settings and releases cancellation even after cleanup faults.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            try
            {
                await connection.DisposeAsync();
            }
            finally { database.ChangeTracker.Clear(); deadline.Dispose(); }
        }
    }
}

/// <summary>Preserves immutable operation time independently of mutable tracked rows.</summary>
internal readonly record struct BrowserDeadline(long Created, long Observed, long Expires)
{
    /// <summary>Applies original operation bounds at the final precommit observation.</summary>
    internal bool Current(long now) => BrowserSessionPolicy.Current(Created, Observed, Expires, now);
}
