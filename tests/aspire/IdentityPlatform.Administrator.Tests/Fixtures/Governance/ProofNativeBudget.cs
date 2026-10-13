using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Captures only safe witnesses for the exact native fields intentionally changed when the fifth product failure locks an account.</summary>
internal sealed record ProofNativeBudget(string ConcurrencyDigest, DateTimeOffset? LockoutUntil)
{
    /// <summary>Normalizes only detached fixture data and only for the explicit fifth-attempt case; all other native user fields remain covered.</summary>
    internal static ProofNativeBudget Capture(PlatformUser user, bool permitsLockout)
    {
        Assert.False(string.IsNullOrEmpty(user.ConcurrencyStamp));
        var result = new ProofNativeBudget(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(user.ConcurrencyStamp))),
            user.LockoutEnd);
        if (permitsLockout)
        {
            user.ConcurrencyStamp = null;
            user.LockoutEnd = null;
        }
        return result;
    }

    /// <summary>Checks the real original-session validator after committed lockout and requires its denial to leave the full state unchanged.</summary>
    internal static async Task RequireSessionDeniedAsync(GovernanceScenario scenario)
    {
        var before = await scenario.SnapshotAsync();
        await using (var scope = scenario.CreateScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<JourneySessions>().ValidateAsync(
                scenario.ActorId, scenario.Grant.SessionId, TestContext.Current.CancellationToken));
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
