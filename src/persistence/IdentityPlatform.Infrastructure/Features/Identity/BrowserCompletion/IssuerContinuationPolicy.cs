using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Owns fixed continuation bounds and canonical decoded-byte selector hashing without interpreting representation as authority.</summary>
internal static class IssuerContinuationPolicy
{
    internal const string Pending = "pending";
    internal const string Completed = "completed";
    internal const string Attached = "attached";
    internal const int LifetimeSeconds = 300;
    internal const int ClockToleranceSeconds = 30;
    internal const int LiveLimit = 128;
    internal const int RetainedLimit = 10000;
    internal const int CleanupLimit = 100;
    internal const int DigestLength = 64;
    internal const int ReferenceLength = 400;
    internal const int StateLength = 32;
    /// <summary>Uses the shared canonical decoded-byte digest for durable lookup.</summary>
    internal static string? Digest(string? value) => BrowserLoginSelector.Digest(value);

    /// <summary>Creates a separate random completion proof whose digest alone is retained.</summary>
    internal static string CreateSelector() => BrowserLoginSelector.CreateSelector();

    /// <summary>Checks the original bounds without changing tracked state after the final persistence call.</summary>
    internal static bool IsCurrent(IssuerContinuation row, long observed) => row.CreatedAt > 0 &&
        row.ExpiresAt > row.CreatedAt && row.ExpiresAt - row.CreatedAt <= LifetimeSeconds &&
        row.ObservedAt >= row.CreatedAt && observed >= row.ObservedAt - ClockToleranceSeconds &&
        row.Revision > 0 && Math.Max(row.ObservedAt, observed) < row.ExpiresAt;

    /// <summary>Records a valid monotonic observation before persistence; final checks use the nonmutating predicate.</summary>
    internal static bool Observe(IssuerContinuation row, long observed)
    {
        if (!IsCurrent(row, observed))
        {
            return false;
        }
        row.ObservedAt = Math.Max(row.ObservedAt, observed);
        return true;
    }

    /// <summary>Limits anonymous commit to a fresh pending row with no interaction, account, completion or native attachment.</summary>
    internal static bool IsStart(IssuerContinuation row) => row.State == Pending && row.Revision == 1 &&
        IsDigest(row.IntentDigest) && IsDigest(row.BrowserDigest) && row.IntentDigest != row.BrowserDigest &&
        row.ClientId == AdministratorProtocolIdentity.ClientId && row.CorrelationId != Guid.Empty &&
        row.ExpiresAt > row.CreatedAt && row.ExpiresAt - row.CreatedAt <= LifetimeSeconds && row.RetainUntil - row.ExpiresAt == ClockToleranceSeconds &&
        row.InteractionId is null && row.InteractionBrowserDigest is null && row.BrowserBudgetId is null && row.UserId is null &&
        row.SessionId is null && row.SessionExpiresAt is null && row.CompletionDigest is null && row.RequestId is null;
    /// <summary>Rejects noncanonical stored digest representations at the anonymous persistence boundary.</summary>
    private static bool IsDigest(string value) => value.Length == DigestLength &&
        value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
