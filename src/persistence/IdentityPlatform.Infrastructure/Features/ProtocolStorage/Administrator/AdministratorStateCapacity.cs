using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Counts actual isolated native state rows under Sessions coordination, with bounded expiry-only reclamation.</summary>
internal sealed class AdministratorStateCapacity
{
    internal const int LiveLimit = 1024;
    internal const int RetainedLimit = 10000;
    internal const int LifetimeSeconds = 300;
    private readonly ProtocolStorageOwner owner;
    private readonly ProtocolStoragePhase phase;
    private readonly AdministratorStateCounts before;
    private readonly DateTimeOffset observed;
    private readonly TimeSpan skew;
    internal bool HasCapacity => before.CanCreate;

    /// <summary>Binds a one-row reservation to the exact transaction epoch and its nondecreasing local time observation.</summary>
    private AdministratorStateCapacity(ProtocolStorageOwner owner, AdministratorStateCounts before, DateTimeOffset observed, TimeSpan skew)
    {
        this.owner = owner;
        phase = owner.Capture(write: true);
        this.before = before;
        this.observed = observed;
        this.skew = skew;
    }

    /// <summary>Requires the caller's fresh Sessions lock, then reclaims at most one batch without deleting live or related tokens.</summary>
    internal static ValueTask<AdministratorStateCapacity?> ReserveLockedAsync(ProtocolStorageOwner owner, TimeSpan skew, CancellationToken token) =>
        owner.RunAsync<AdministratorStateCapacity?>(async cancellation =>
        {
            if (owner.Database is not AdminSessionDbContext database)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var now = owner.Clock.GetUtcNow();
            if (!AdministratorTokenBudget.TryCutoffs(now, skew, out var retention, out var expiration))
            {
                return null;
            }
            await CleanupAsync(database, retention, expiration, cancellation);
            return new(owner, await ReadAsync(database, expiration, cancellation), now, skew);
        }, token, write: true);

    /// <summary>Reconciles actual native writes before commit; unknown rows consume capacity and unexpected deltas cannot be accepted.</summary>
    internal ValueTask<bool> CanCommitAsync(bool created, CancellationToken token)
    {
        owner.Check(phase, token);
        return owner.RunAsync(async cancellation =>
        {
            var sample = owner.Clock.GetUtcNow();
            if (sample < observed - TimeSpan.FromSeconds(AdministratorAssertion.ClockSkewSeconds) || (created && !HasCapacity) ||
                !AdministratorTokenBudget.TryCutoffs(sample > observed ? sample : observed, skew, out _, out var expiration))
            {
                return false;
            }
            var current = await ReadAsync(owner.Database, expiration, cancellation);
            owner.Check(phase, cancellation);
            return current.Retained == before.Retained + (created ? 1 : 0) &&
                (!created || current.WithinLimits) && owner.Clock.GetUtcNow() >= observed - TimeSpan.FromSeconds(AdministratorAssertion.ClockSkewSeconds);
        }, token, write: true);
    }

    /// <summary>Bounds materialization and treats every unknown native row/status/expiry as live; B1 tickets occupy a different table.</summary>
    private static async Task<AdministratorStateCounts> ReadAsync(DbContext database, DateTime expiration, CancellationToken token)
    {
        var tokens = database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking();
        var count = await tokens.LongCountAsync(token);
        if (count > RetainedLimit)
        {
            return new(count, LiveLimit + 1);
        }
        var rows = await tokens.Select(row => new { row.Type, row.Status, row.ExpirationDate }).Take(RetainedLimit + 1).ToArrayAsync(token);
        if (rows.LongLength != count)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var live = rows.Count(row => !IsState(row.Type) ||
            (!Terminal(row.Status) && (row.ExpirationDate is null || row.ExpirationDate >= expiration)));
        return new(count, live);
    }

    /// <summary>Selects at most one bounded expired batch, applying native type casing in memory before exact-ID predicate rechecks.</summary>
    private static async Task CleanupAsync(DbContext database, DateTime retention, DateTime expiration, CancellationToken token)
    {
        var expired = database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Application == null && row.Authorization == null &&
            row.CreationDate < retention && row.ExpirationDate < expiration);
        var candidates = await expired.AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Type })
            .Take(AdministratorTokenBudget.CleanupLimit).ToArrayAsync(token);
        foreach (var row in candidates.Where(row => IsState(row.Type)))
        {
            await expired.Where(current => current.Id == row.Id && current.Type == row.Type).ExecuteDeleteAsync(token);
        }
    }

    /// <summary>Uses the same native type semantics on both database providers for counting and bounded cleanup eligibility.</summary>
    private static bool IsState(string? type) => string.Equals(type, OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>Accepts only known native terminal statuses as non-live, independently of database collation.</summary>
    private static bool Terminal(string? status) =>
        string.Equals(status, OpenIddictConstants.Statuses.Redeemed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Applies exact hard limits without overflow or negative-count admission.</summary>
internal readonly record struct AdministratorStateCounts(long Retained, long Live)
{
    internal bool WithinLimits => Retained >= 0 && Live >= 0 && Live <= Retained &&
        Retained <= AdministratorStateCapacity.RetainedLimit && Live <= AdministratorStateCapacity.LiveLimit;
    internal bool CanCreate => WithinLimits && Retained < AdministratorStateCapacity.RetainedLimit && Live < AdministratorStateCapacity.LiveLimit;
}
