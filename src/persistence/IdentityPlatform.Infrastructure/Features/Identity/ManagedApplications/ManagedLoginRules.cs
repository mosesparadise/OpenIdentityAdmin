using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Owns finite managed interaction budgets independently of administrator sessions and protocol quotas.</summary>
internal static class ManagedLoginRules
{
    internal const int LifetimeSeconds = 300;
    internal const int BrowserLimit = 8;
    internal const int LiveLimit = 128;
    internal const int RetainedLimit = 10000;
    internal const int CleanupLimit = 100;
    internal const int TokenLimit = 20000;
    internal const int AuthorizationLimit = 2000;
    internal const int RequestLimit = 10000;
    internal const int RetentionDays = 14;
    internal const int RollbackSeconds = 30;
    private const int SecretBytes = 32;
    internal const int SecretLength = SecretBytes * 2;

    /// <summary>Creates an unpredictable selector or browser proof without printable record formatting.</summary>
    internal static string CreateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>Accepts only the exact canonical random-proof representation.</summary>
    internal static bool IsSecret(string? value) => value is { Length: SecretLength } && value.All(character =>
        character is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>Stores only a fixed digest of the browser proof or opaque selector.</summary>
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Never lowers authority time and rejects clock rollback beyond the existing native tolerance.</summary>
    internal static long Observe(TimeProvider clock, long watermark)
    {
        var sample = clock.GetUtcNow().ToUnixTimeSeconds();
        if (sample < 0 || watermark < 0 || watermark - sample > RollbackSeconds)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return Math.Max(sample, watermark);
    }

    /// <summary>Retains tombstones beyond minimum age and every known native deadline, including skew and rollback allowance.</summary>
    internal static long RetainUntil(long created, long expiration, TimeSpan skew)
    {
        if (skew < TimeSpan.Zero || skew.TotalSeconds > LifetimeSeconds)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return checked(Math.Max(created + (long)TimeSpan.FromDays(RetentionDays).TotalSeconds,
            expiration + (long)Math.Ceiling(skew.TotalSeconds) + RollbackSeconds));
    }
}
