using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Reserves native creation by counting actual fixed-application rows inside the profile-locked issuance transaction.</summary>
internal sealed class AdministratorTokenBudget
{
    internal const int TokenLimit = 20000;
    internal const int AuthorizationLimit = 2000;
    internal const int RequestLimit = 10000;
    internal const int LiveRequestLimit = 128;
    internal const int CleanupLimit = 100;
    internal const int RetentionDays = 14;
    private readonly ProtocolStorageOwner owner;
    private readonly ProtocolStoragePhase phase;
    private readonly string application;
    private readonly AdministratorTokenCounts before;
    private readonly AdministratorTokenCounts addition;
    private readonly AdministratorProtocolProfile profile;
    private readonly long revision;
    private readonly long observed;
    private readonly TimeSpan validationSkew;
    internal bool HasCapacity => before.CanAdd(addition);

    /// <summary>Binds the expected native row delta to one owner epoch, never an independent counter or reusable capacity token.</summary>
    private AdministratorTokenBudget(ProtocolStorageOwner owner, AdministratorProtocolProfile profile, long observed, TimeSpan validationSkew,
        AdministratorTokenCounts before, AdministratorTokenCounts addition)
    {
        this.owner = owner;
        phase = owner.Capture(write: true);
        application = profile.ApplicationId;
        this.profile = profile;
        revision = profile.Revision;
        this.observed = observed;
        this.validationSkew = validationSkew;
        this.before = before;
        this.addition = addition;
    }

    /// <summary>Requires prior profile ownership, reclaims only safely expired rows, then reserves the exact planned native creation delta.</summary>
    internal static ValueTask<AdministratorTokenBudget?> ReserveLockedAsync(ProtocolStorageOwner owner, string application,
        AdministratorTokenCreation creation, TimeSpan validationSkew, CancellationToken token) => owner.RunAsync<AdministratorTokenBudget?>(async cancellation =>
        {
            var phase = owner.Capture(write: true);
            var profile = await owner.Database.Set<AdministratorProtocolProfile>().SingleOrDefaultAsync(cancellation);
            if (profile is not { Enabled: true } || profile.ApplicationId != application ||
                !TryObserve(owner.Clock, profile.ObservedAt, out var now) ||
                !TryCutoffs(DateTimeOffset.FromUnixTimeSeconds(now), validationSkew, out var retention, out var expiration))
            {
                return null;
            }
            await AdministratorTokenCleanup.RunAsync(owner.Database, application, retention, expiration, cancellation);
            var before = await ReadAsync(owner.Database, application, expiration, cancellation);
            var addition = AdministratorTokenCounts.For(creation);
            owner.Check(phase, cancellation);
            return new(owner, profile, now, validationSkew, before, addition);
        }, token, write: true);

    /// <summary>Requires exact native persistence deltas and all hard ceilings before commit; partial or extra native creation cannot succeed.</summary>
    internal ValueTask<bool> IsCurrentAsync(CancellationToken token) => CheckAsync(issued: true, token);

    /// <summary>Allows denied-capacity cleanup progress only with zero native creation and the same final nondecreasing clock proof.</summary>
    internal ValueTask<bool> CanCommitCleanupAsync(CancellationToken token) => CheckAsync(issued: false, token);

    /// <summary>Reconciles exact rows and persists a checked watermark before either cleanup-only or issuance commit.</summary>
    private ValueTask<bool> CheckAsync(bool issued, CancellationToken token)
    {
        owner.Check(phase, token);
        return owner.RunAsync(async cancellation =>
        {
            if ((issued && !HasCapacity) || !profile.Enabled || profile.Revision != revision || profile.ApplicationId != application ||
                owner.Database.Entry(profile).State == EntityState.Detached ||
                !TryObserve(owner.Clock, Math.Max(observed, profile.ObservedAt), out var now) ||
                !TryCutoffs(DateTimeOffset.FromUnixTimeSeconds(now), validationSkew, out _, out var expiration))
            {
                return false;
            }
            profile.ObservedAt = now;
            await owner.Database.SaveChangesAsync(cancellation);
            var current = await ReadAsync(owner.Database, application, expiration, cancellation);
            owner.Check(phase, cancellation);
            var expected = issued ? addition : default;
            if (current.Tokens != before.Tokens + expected.Tokens || current.Authorizations != before.Authorizations + expected.Authorizations ||
                current.Requests != before.Requests + expected.Requests || (issued && !current.CanAdd(default)))
            {
                return false;
            }
            return TryObserve(owner.Clock, now, out _);
        }, token, write: true);
    }

    /// <summary>Rejects negative time and rollback beyond the approved tolerance; accepted smaller samples never lower authority time.</summary>
    private static bool TryObserve(TimeProvider clock, long watermark, out long now)
    {
        var sample = clock.GetUtcNow().ToUnixTimeSeconds();
        now = Math.Max(sample, watermark);
        return sample >= 0 && watermark >= 0 && watermark - sample <= AdministratorAssertion.ClockSkewSeconds;
    }

    /// <summary>Counts all statuses/types; generic native request rows conservatively consume both PAR budgets.</summary>
    private static async Task<AdministratorTokenCounts> ReadAsync(DbContext database, string application, DateTime expiration, CancellationToken token)
    {
        var tokens = database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == application);
        var authorizations = database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == application);
        var count = await tokens.LongCountAsync(token);
        var authorizationCount = await authorizations.LongCountAsync(token);
        if (count > TokenLimit)
        {
            return new(count, authorizationCount, RequestLimit + 1, LiveRequestLimit + 1);
        }
        var rows = await tokens.Select(row => new { row.Type, row.Status, row.ExpirationDate }).Take(TokenLimit + 1).ToArrayAsync(token);
        if (rows.LongLength != count)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var requests = rows.Where(row => string.Equals(row.Type, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, StringComparison.OrdinalIgnoreCase)).ToArray();
        var live = requests.Count(row => !Terminal(row.Status) && (row.ExpirationDate == null || row.ExpirationDate >= expiration));
        return new(count, authorizationCount, requests.Length, live);
    }

    /// <summary>Matches native status casing without provider collation dependence; unknown status remains conservatively live.</summary>
    private static bool Terminal(string? status) =>
        string.Equals(status, OpenIddictConstants.Statuses.Redeemed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase);

    /// <summary>Retains the native Quartz default minimum age explicitly, plus configured validation skew and allowed cross-instance clock rollback.</summary>
    internal static bool TryCutoffs(DateTimeOffset now, TimeSpan skew, out DateTime retention, out DateTime expiration)
    {
        retention = expiration = default;
        var grace = TimeSpan.FromSeconds(AdministratorAssertion.ClockSkewSeconds);
        var age = TimeSpan.FromDays(RetentionDays);
        if (skew < TimeSpan.Zero || skew > now.UtcDateTime - DateTime.MinValue - grace || now.UtcDateTime - DateTime.MinValue < age)
        {
            return false;
        }
        retention = now.UtcDateTime - age;
        expiration = now.UtcDateTime - skew - grace;
        return true;
    }
}

/// <summary>Enumerates only the native artifacts permitted by the fixed administrator endpoints.</summary>
internal enum AdministratorTokenCreation
{
    PushedRequest, AuthorizationCode, TokenExchange
}

/// <summary>Provides overflow-safe hard-cap arithmetic over bounded database counts and the fixed native creation plans.</summary>
internal readonly record struct AdministratorTokenCounts(long Tokens, long Authorizations, long Requests, long LiveRequests)
{
    private const int AccessAndIdentityTokens = 2;
    /// <summary>Includes the ad-hoc authorization created with a fresh code, and both access and identity entries at exchange.</summary>
    internal static AdministratorTokenCounts For(AdministratorTokenCreation creation) => creation switch
    {
        AdministratorTokenCreation.PushedRequest => new(1, 0, 1, 1),
        AdministratorTokenCreation.AuthorizationCode => new(1, 1, 0, 0),
        AdministratorTokenCreation.TokenExchange => new(AccessAndIdentityTokens, 0, 0, 0),
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Accepts exact capacity equality only when the entire requested native delta still fits every applicable limit.</summary>
    internal bool CanAdd(AdministratorTokenCounts value) =>
        Fits(Tokens, value.Tokens, AdministratorTokenBudget.TokenLimit) && Fits(Authorizations, value.Authorizations, AdministratorTokenBudget.AuthorizationLimit) &&
        Fits(Requests, value.Requests, AdministratorTokenBudget.RequestLimit) && Fits(LiveRequests, value.LiveRequests, AdministratorTokenBudget.LiveRequestLimit);

    /// <summary>Subtracts only after validating both nonnegative operands, preventing wraparound from admitting malformed counts.</summary>
    private static bool Fits(long current, long addition, long limit) => current >= 0 && addition >= 0 && addition <= limit && current <= limit - addition;
}
