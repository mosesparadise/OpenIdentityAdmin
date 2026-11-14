using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Writes the complete original managed-interaction shape against historical schemas without consulting the evolving EF model.</summary>
internal static class ManagedLegacyRows
{
    private const string Insert = "INSERT INTO \"platform\".\"ManagedLoginInteraction\" (" +
        "\"SelectorDigest\", \"BrowserDigest\", \"ApplicationId\", \"DemandDigest\", \"RequestId\", " +
        "\"ApplicationRevision\", \"Revision\", \"State\", \"CreatedAt\", \"ExpiresAt\", " +
        "\"ObservedAt\", \"RetainUntil\", \"RequestExpiresAt\", \"NativeDependencyExpiresAt\", \"NativeDependencyCreatedAt\", " +
        "\"CorrelationId\", \"CeremonyId\", \"CeremonyBrowserDigest\", \"BrowserBudgetId\", \"UserId\", " +
        "\"SecurityStamp\", \"AuthenticationGeneration\", \"FactorGeneration\", \"EnrollmentRevision\", \"PasswordVerifiedAt\", " +
        "\"MfaVerifiedAt\", \"ConsentVerifiedAt\", \"AuthorizationId\", \"CodeId\") " +
        "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, {16}, {17}, {18}, {19}, {20}, {21}, {22}, {23}, {24}, {25}, {26}, {27}, {28})";

    /// <summary>Preserves all original phase, proof, lineage and retention fields using provider-parameterized values.</summary>
    internal static Task<int> InsertAsync(PlatformDbContext database, ManagedLoginInteraction row, CancellationToken token) =>
        database.Database.ExecuteSqlRawAsync(Insert,
            new object?[] { row.SelectorDigest, row.BrowserDigest, row.ApplicationId, row.DemandDigest, row.RequestId,
             row.ApplicationRevision, row.Revision, (int)row.State, row.CreatedAt, row.ExpiresAt,
             row.ObservedAt, row.RetainUntil, row.RequestExpiresAt, row.NativeDependencyExpiresAt, row.NativeDependencyCreatedAt,
             row.CorrelationId, row.CeremonyId, row.CeremonyBrowserDigest, row.BrowserBudgetId, row.UserId,
             row.SecurityStamp, row.AuthenticationGeneration, row.FactorGeneration, row.EnrollmentRevision, row.PasswordVerifiedAt,
             row.MfaVerifiedAt, row.ConsentVerifiedAt, row.AuthorizationId, row.CodeId }.Select(value => value ?? DBNull.Value), token);
}
