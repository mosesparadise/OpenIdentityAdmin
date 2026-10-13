using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Hashes existing columns only, so the same exact witness is readable before and after the additive migration.</summary>
internal static class GovernanceUpgradeSnapshot
{
    /// <summary>Reuses security-state coverage and adds permanent receipt, native grants and auxiliary native tables.</summary>
    internal static async Task<string> ReadAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var existing = new
        {
            Security = await SecuritySnapshot.ReadAsync(database),
            Governance = await database.Set<AdministratorGovernance>().AsNoTracking().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Revision, row.BootstrapConsumed }).ToArrayAsync(token),
            Receipts = await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Roles = await database.Roles.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Memberships = await database.UserRoles.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.RoleId).ToArrayAsync(token),
            RoleClaims = await database.RoleClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            UserClaims = await database.UserClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            UserLogins = await database.UserLogins.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.LoginProvider).ThenBy(row => row.ProviderKey).ToArrayAsync(token),
            Tokens = await database.UserTokens.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.LoginProvider).ThenBy(row => row.Name).ToArrayAsync(token),
            Passkeys = await database.Set<IdentityUserPasskey<string>>().AsNoTracking().OrderBy(row => row.CredentialId).ToArrayAsync(token),
            LocalSessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Admission = await database.Set<AdministratorAdmission>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Browsers = await database.Set<AdministratorBrowserBudget>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(existing)));
    }
}
