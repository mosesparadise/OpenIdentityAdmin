using System.Globalization;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Captures a trusted asynchronous durable read for synchronous native antiforgery; never binds directly from submitted generation data.</summary>
internal sealed class BrowserRequestBinding
{
    private const string Separator = ":";
    private const string Purpose = "browser-mutation-v1";
    internal string AdditionalData
    {
        get;
    }

    /// <summary>Keeps the exact browser digest and generation in native protected additional data, distinct from authentication authority.</summary>
    private BrowserRequestBinding(string digest, long generation, string purpose = Purpose) =>
        AdditionalData = string.Join(Separator, purpose, digest, generation.ToString(CultureInfo.InvariantCulture));

    private const string ReconciliationPurpose = "browser-logout-reconciliation-v1";

    /// <summary>Binds only a verified immediate logout successor to its original generation in a separate native antiforgery purpose.</summary>
    internal static BrowserRequestBinding? FromLogoutReconciliation(BrowserGenerationProof proof, BrowserLogoutResult result, DateTimeOffset now) =>
        BrowserSessionPolicy.IsDigest(proof.Digest) && proof.Generation > 0 && proof.Generation < long.MaxValue &&
        result.Generation == proof.Generation + 1 && !result.RemoteReserved &&
        BrowserSessionPolicy.Current(result.ObservedAt, result.ObservedAt, result.ExpiresAt, now.UtcTicks)
            ? new(proof.Digest, proof.Generation, ReconciliationPurpose) : null;

    /// <summary>Allows zero only after a confirmed absent row for a newly issued browser proof; missing later generations never normalize to zero.</summary>
    internal static BrowserRequestBinding? FromRead(string browserDigest, long expectedGeneration, BrowserContext? row, DateTimeOffset now)
    {
        if (!BrowserSessionPolicy.IsDigest(browserDigest) || expectedGeneration < 0)
        {
            return null;
        }
        if (row is null)
        {
            return expectedGeneration == 0 ? new(browserDigest, 0) : null;
        }
        return row.Digest == browserDigest && BrowserSessionPolicy.Matches(row, expectedGeneration, now.UtcTicks)
            ? new(browserDigest, expectedGeneration) : null;
    }
}
