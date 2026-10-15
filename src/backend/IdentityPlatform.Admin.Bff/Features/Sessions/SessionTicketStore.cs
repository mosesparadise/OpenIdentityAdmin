using System.Data.Common;
using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Exposes an internal protected store with explicit unavailable/failure outcomes and no HTTP or authentication authority.</summary>
internal sealed class SessionTicketStore(IServiceScopeFactory scopes, SessionTicketCodec codec, TimeProvider clock)
{
    /// <summary>Generates a fresh reference and releases it only after protected state and intent durably commit.</summary>
    internal Task<SessionTicketResult> CreateAsync(AuthenticationTicket ticket, string subject, string sourceSession,
        DateTimeOffset sourceExpiry, CancellationToken cancellationToken, TimeSpan? idle = null, TimeSpan? absolute = null) =>
        ExecuteAsync(async persistence =>
        {
            var reference = SessionHandle.Create();
            var row = SessionTicketPolicy.Create(reference.Digest, subject, sourceSession, clock.GetUtcNow(), sourceExpiry,
                idle ?? SessionTicketLimits.MaximumIdle, absolute ?? SessionTicketLimits.MaximumAbsolute);
            row.ProtectedTicket = codec.Protect(row, ticket);
            return await persistence.CreateAsync(row, cancellationToken)
                ? new(SessionTicketStatus.Available, reference.Handle, row.Revision) : new(SessionTicketStatus.Unavailable);
        }, cancellationToken);

    /// <summary>Returns a validated private ticket without renewing activity; any rejection or failure contains no ticket.</summary>
    internal Task<SessionTicketResult> LoadAsync(string handle, CancellationToken cancellationToken) =>
        ExecuteAsync(async persistence =>
        {
            if (!SessionHandle.TryDigest(handle, out var digest))
            {
                return new(SessionTicketStatus.Unavailable);
            }
            var row = await persistence.FindAsync(digest, cancellationToken);
            if (row is null)
            {
                return new(SessionTicketStatus.Unavailable);
            }
            var ticket = codec.Unprotect(row);
            return ticket is not null && SessionTicketPolicy.IsLive(row, clock.GetUtcNow())
                ? new(SessionTicketStatus.Available, revision: row.Revision, ticket: ticket) : new(SessionTicketStatus.Unavailable);
        }, cancellationToken);

    /// <summary>Replaces only a validated live expected revision; immutable binding/policy comes from the authenticated original.</summary>
    internal Task<SessionTicketResult> RenewAsync(string handle, long expectedRevision, AuthenticationTicket ticket, CancellationToken cancellationToken) =>
        ExecuteAsync(async persistence =>
        {
            if (!SessionHandle.TryDigest(handle, out var digest))
            {
                return new(SessionTicketStatus.Unavailable);
            }
            var row = await persistence.FindAsync(digest, cancellationToken);
            if (row is null || row.Revision != expectedRevision || codec.Unprotect(row) is null ||
                !SessionTicketPolicy.IsLive(row, clock.GetUtcNow()))
            {
                return new(SessionTicketStatus.Unavailable);
            }
            var replacement = SessionTicketPolicy.Renew(row, clock.GetUtcNow());
            replacement.ProtectedTicket = codec.Protect(replacement, ticket);
            return await persistence.RenewAsync(row, replacement, cancellationToken)
                ? new(SessionTicketStatus.Available, revision: replacement.Revision) : new(SessionTicketStatus.Unavailable);
        }, cancellationToken);

    /// <summary>Commits irreversible local denial, treating absent/already-revoked references idempotently without remote claims.</summary>
    internal Task<SessionTicketResult> RevokeAsync(string handle, CancellationToken cancellationToken) =>
        ExecuteAsync(async persistence =>
        {
            if (SessionHandle.TryDigest(handle, out var digest))
            {
                await persistence.RevokeAsync(digest, cancellationToken);
            }
            return new(SessionTicketStatus.Available);
        }, cancellationToken);

    /// <summary>Removes one bounded eligible batch; audit retention is deliberately independent of ticket cleanup.</summary>
    internal Task<SessionTicketResult> CleanupAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(async persistence => new(SessionTicketStatus.Available,
            removed: await persistence.CleanupAsync(clock.GetUtcNow(), cancellationToken)), cancellationToken);

    /// <summary>Owns a fresh scope for each operation and exposes only finite failures; cancellation always propagates.</summary>
    private async Task<SessionTicketResult> ExecuteAsync(Func<SessionTicketPersistence, Task<SessionTicketResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
            return await operation(new SessionTicketPersistence(database, clock));
        }
        catch (ArgumentException) { return new(SessionTicketStatus.Unavailable); }
        catch (Exception exception) when (exception is DbException or DbUpdateException or CryptographicException or IOException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(SessionTicketStatus.Failed);
        }
    }
}

/// <summary>Distinguishes rejected state from operational uncertainty without revealing private exception data.</summary>
internal enum SessionTicketStatus
{
    Available, Unavailable, Failed
}

/// <summary>Contains private state only on successful operations; avoid generated string representations or logging this object.</summary>
internal sealed class SessionTicketResult(SessionTicketStatus status, string? handle = null, long revision = 0,
    AuthenticationTicket? ticket = null, int removed = 0)
{
    internal SessionTicketStatus Status { get; } = status;
    internal string? Handle { get; } = handle;
    internal long Revision { get; } = revision;
    internal AuthenticationTicket? Ticket { get; } = ticket;
    internal int Removed { get; } = removed;
}
