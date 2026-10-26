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
    /// <summary>Captures the historical security shape plus permanent receipt, native grants and auxiliary native tables.</summary>
    internal static async Task<string> ReadAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var existing = new
        {
            Security = await ReadSecurityAsync(database),
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

    /// <summary>Freezes historical security surfaces without using the current-schema rollback witness against a downgraded database.</summary>
    private static async Task<object> ReadSecurityAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        return new
        {
            Users = await database.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Enrollments = await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Interactions = await ReadInteractionsAsync(database),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Factors = await database.Set<AdministratorFactor>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Recovery = await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Replay = await database.Set<AdministratorReplay>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Audit = await HistoricalSecurityAudit.ReadAsync(database, token)
        };
    }

    /// <summary>Projects all 21 pre-continuation fields explicitly so the provider never selects the later nullable marker.</summary>
    private static async Task<AdministratorInteraction[]> ReadInteractionsAsync(PlatformDbContext database) =>
        await database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.Id).Select(row => new AdministratorInteraction
        {
            Id = row.Id,
            PublicId = row.PublicId,
            UserId = row.UserId,
            BrowserBudgetId = row.BrowserBudgetId,
            BrowserDigest = row.BrowserDigest,
            Purpose = row.Purpose,
            State = row.State,
            SecurityStamp = row.SecurityStamp,
            Generation = row.Generation,
            DestinationGeneration = row.DestinationGeneration,
            ExpectedEnrollmentRevision = row.ExpectedEnrollmentRevision,
            AuthorizationKind = row.AuthorizationKind,
            AuthorizationVerifiedAt = row.AuthorizationVerifiedAt,
            SourceFactorGeneration = row.SourceFactorGeneration,
            SessionPublicId = row.SessionPublicId,
            CreatedAt = row.CreatedAt,
            ExpiresAt = row.ExpiresAt,
            PasswordVerifiedAt = row.PasswordVerifiedAt,
            Attempts = row.Attempts,
            Revision = row.Revision,
            StagedSeed = row.StagedSeed
        }).ToArrayAsync(TestContext.Current.CancellationToken);
}
