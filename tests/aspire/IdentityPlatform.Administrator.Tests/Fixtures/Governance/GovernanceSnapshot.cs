using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Separates nonsecret hashes of authority, security and native auxiliary state for exact rollback assertions.</summary>
/// <remarks>Capture only after all writers have joined; sequential detached queries are not a concurrent snapshot-isolation oracle.</remarks>
internal sealed record GovernanceSnapshot(string Authority, string Security, string Auxiliary)
{
    /// <summary>Extends the existing security snapshot with native role authority and the governance revision singleton.</summary>
    internal static async Task<GovernanceSnapshot> ReadAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var authority = new
        {
            Roles = await database.Roles.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Memberships = await database.UserRoles.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.RoleId).ToArrayAsync(token),
            Claims = await database.RoleClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Governance = await database.Set<AdministratorGovernance>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return new GovernanceSnapshot(Hash(authority), await SecuritySnapshot.ReadAsync(database), await AuxiliaryAsync(database));
    }

    /// <summary>Covers non-authoritative native claim, login and token tables so denied operations cannot silently create fallback credentials.</summary>
    private static async Task<string> AuxiliaryAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var state = new
        {
            LocalSessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Claims = await database.UserClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Logins = await database.UserLogins.AsNoTracking().OrderBy(row => row.LoginProvider).ThenBy(row => row.ProviderKey).ToArrayAsync(token),
            Tokens = await database.UserTokens.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.LoginProvider)
                .ThenBy(row => row.Name).ToArrayAsync(token)
        };
        return Hash(state);
    }

    /// <summary>Returns only a digest so assertion failures never serialize credential material or raw audit payloads.</summary>
    private static string Hash<T>(T state) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
}
