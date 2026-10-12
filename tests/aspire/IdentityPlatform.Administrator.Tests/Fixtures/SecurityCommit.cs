using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Observes independent committed audit state while the sending client deliberately never reads its HTTP response.</summary>
internal static class SecurityCommit
{
    /// <summary>Waits for one specific successful audited action within a bounded deadline without reading headers, cookies or response body.</summary>
    internal static async Task ObserveAsync(PlatformDbContext database, string action)
    {
        const int DeadlineSeconds = 10;
        const int PollMilliseconds = 25;
        const string Accepted = "accepted";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        while (!await database.Set<SecurityAuditIntent>().AnyAsync(row => row.Event == action && row.Outcome == Accepted, deadline.Token))
        {
            await Task.Delay(PollMilliseconds, deadline.Token);
        }
    }

    /// <summary>Hashes exact recovery-verifier storage for replay comparisons that never print secret-derived material.</summary>
    internal static async Task<string> RecoveryDigestAsync(PlatformDbContext database) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(await SecuritySnapshot.RecoveryAsync(database))));
}
