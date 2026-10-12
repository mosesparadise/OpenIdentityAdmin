using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Produces nonsecret rollback witnesses over complete native/product state without printing underlying credential material.</summary>
internal static class SecuritySnapshot
{
    /// <summary>Captures every security mutation surface in deterministic order for exact atomicity comparisons.</summary>
    internal static async Task<string> ReadAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var state = new
        {
            Users = await database.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Enrollments = await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Interactions = await database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Factors = await database.Set<AdministratorFactor>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Recovery = await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Replay = await database.Set<AdministratorReplay>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Audit = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }

    /// <summary>Captures exact recovery-row identity, generation, verifier and claim state without exposing it to assertion diagnostics.</summary>
    internal static async Task<AdministratorRecoveryCode[]> RecoveryAsync(PlatformDbContext database) =>
        await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    /// <summary>Requires precisely one expected verifier claim and byte-for-byte preservation of every other verifier field.</summary>
    internal static void RequireSingleClaim(AdministratorRecoveryCode[] before, AdministratorRecoveryCode[] after, string code)
    {
        Assert.Equal(before.Length, after.Length);
        const string Changed = "Recovery changed verifier identity, generation or remaining material beyond the single authorized claim.";
        var selected = before.Single(row => row.Verifier == FactorOracle.Verifier(row.UserId, row.Generation, code));
        foreach (var original in before)
        {
            var current = after.Single(row => row.Id == original.Id);
            Assert.True(original.UserId == current.UserId && original.Generation == current.Generation && original.Verifier == current.Verifier, Changed);
            if (original.Id == selected.Id)
            {
                Assert.Null(original.RedeemedAt);
                Assert.NotNull(current.RedeemedAt);
            }
            else
            {
                Assert.Equal(original.RedeemedAt, current.RedeemedAt);
            }
        }
    }
}
