using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Retains one original interaction reservation and its exact native delta inside a governance-locked cache transaction.</summary>
internal sealed class ManagedLoginReservation
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStoragePhase phase;
    private readonly ManagedLoginInteraction row;
    private readonly ManagedNativeBudget budget;
    private readonly long cutoff;
    private readonly TimeSpan skew;
    private readonly string initial;
    private string? attached;
    internal long ExpiresAt => row.ExpiresAt;
    internal long CreatedAt => row.CreatedAt;

    /// <summary>Captures immutable ownership after the reserved interaction has actually saved before native creation.</summary>
    private ManagedLoginReservation(ProtocolStorageScope scope, ManagedLoginInteraction row, ManagedNativeBudget budget, long cutoff, TimeSpan skew)
    {
        this.scope = scope;
        this.row = row;
        initial = FactorBinding.Digest(row);
        this.budget = budget;
        this.cutoff = cutoff;
        this.skew = skew;
        phase = scope.Owner.Capture(write: true);
    }

    /// <summary>Runs bounded safe cleanup then independently reserves browser, global, retained and native capacity.</summary>
    internal static async ValueTask<ManagedLoginReservation?> CreateAsync(ProtocolStorageScope scope, ManagedRegistration registration,
        string selector, string browser, string demand, TimeSpan skew, CancellationToken token)
    {
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, registration.ObservedAt);
        var expiry = Math.Min(checked(now + ManagedLoginRules.LifetimeSeconds), registration.ExpiresAt);
        _ = ManagedLoginRules.RetainUntil(now, expiry, skew);
        await ManagedLoginCleanup.RunAsync(scope, now, skew, token);
        var cutoff = checked(now - (long)Math.Ceiling(skew.TotalSeconds) - ManagedLoginRules.RollbackSeconds);
        var budget = await ManagedNativeBudget.ReadAsync(scope, cutoff, token);
        var browserDigest = ManagedLoginRules.Digest(browser);
        if (!budget.CanCache || !await HasCapacityAsync(scope, browserDigest, now, token))
        {
            return null;
        }
        var row = new ManagedLoginInteraction
        {
            SelectorDigest = ManagedLoginRules.Digest(selector),
            BrowserDigest = browserDigest,
            DemandDigest = demand,
            ApplicationId = registration.ApplicationId,
            ApplicationRevision = registration.Revision,
            Revision = 1,
            State = ManagedLoginState.PendingPassword,
            CreatedAt = now,
            ObservedAt = now,
            ExpiresAt = expiry,
            RetainUntil = ManagedLoginRules.RetainUntil(now, expiry, skew),
            CorrelationId = Guid.NewGuid()
        };
        var reservation = new ManagedLoginReservation(scope, row, budget, cutoff, skew);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(row);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await reservation.VerifyRowAsync(reservation.initial, token);
        return reservation;
    }

    /// <summary>Counts all retained interactions and every nonterminal live row without per-application capacity bypass.</summary>
    private static ValueTask<bool> HasCapacityAsync(ProtocolStorageScope scope, string browser, long now, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var rows = scope.Owner.Database.Set<ManagedLoginInteraction>();
            var live = rows.Where(value => value.State <= ManagedLoginState.Approved && value.ExpiresAt > now);
            return await rows.LongCountAsync(current) < ManagedLoginRules.RetainedLimit &&
                await live.LongCountAsync(current) < ManagedLoginRules.LiveLimit &&
                await live.LongCountAsync(value => value.BrowserDigest == browser, current) < ManagedLoginRules.BrowserLimit;
        }, token);

    /// <summary>Attaches only the actual native row after verification and rechecks its stored reservation and exact aggregate artifact delta.</summary>
    internal async ValueTask AttachAsync(string requestId, long created, long expires, CancellationToken token)
    {
        scope.Owner.Check(phase, token);
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt);
        if (FactorBinding.Digest(row) != initial || row.RequestId is not null || now >= row.ExpiresAt || created < row.CreatedAt || expires != row.ExpiresAt ||
            !budget.MatchesCache(await ManagedNativeBudget.ReadAsync(scope, cutoff, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        row.RequestId = requestId;
        row.RequestExpiresAt = expires;
        row.NativeDependencyCreatedAt = created;
        row.NativeDependencyExpiresAt = expires;
        row.RetainUntil = ManagedLoginRules.RetainUntil(created, expires, skew);
        row.ObservedAt = now;
        var expected = FactorBinding.Digest(row);
        attached = expected;
        await scope.Owner.RunAsync(async current => { await scope.Owner.Database.SaveChangesAsync(current); }, token, write: true);
        await VerifyRowAsync(expected, token);
        scope.Owner.Check(phase, token);
        if (ManagedLoginRules.Observe(scope.Owner.Clock, now) >= row.ExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
    /// <summary>Reconciles the complete saved attachment and exact native deltas again after the last save and subsequent native proof reads.</summary>
    internal async ValueTask VerifyFinalAsync(CancellationToken token)
    {
        scope.Owner.Check(phase, token);
        await VerifyRowAsync(attached ?? throw ProtocolStorageOwner.Unavailable(), token);
        if (!budget.MatchesCache(await ManagedNativeBudget.ReadAsync(scope, cutoff, token)) ||
            ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt) >= row.ExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Compares both tracked and freshly persisted complete state against a pre-save immutable snapshot, including every absent human-proof field.</summary>
    private async ValueTask VerifyRowAsync(string expected, CancellationToken token)
    {
        var actual = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .AsNoTracking().SingleAsync(value => value.SelectorDigest == row.SelectorDigest, current)), token);
        if (FactorBinding.Digest(actual) != expected || FactorBinding.Digest(row) != expected)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

}
