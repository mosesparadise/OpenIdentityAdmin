using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Seeds independently named pre-retirement policy columns without asking the current EF model to read a nonexistent default column.</summary>
internal static class RetirementPredecessorRows
{
    private const string Insert = "INSERT INTO \"platform\".\"ApplicationPolicy\" (\"ApplicationId\", \"Profile\", \"Enabled\", \"Revision\", \"CredentialRevision\", \"CredentialIssuedAt\", \"CredentialExpiresAt\", \"ObservedAt\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})";

    /// <summary>Inserts exactly the historical tuple and detaches only its staged policy, preserving any associated historical grant/audit entities.</summary>
    internal static async Task InsertAsync(PlatformDbContext database, ApplicationPolicy policy, CancellationToken token)
    {
        Assert.False(policy.Retired);
        database.Entry(policy).State = EntityState.Detached;
        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(database.GetDatabaseProvider().Model.Identifiers(Insert),
            [policy.ApplicationId, policy.Profile, policy.Enabled, policy.Revision, policy.CredentialRevision,
                policy.CredentialIssuedAt!.Value, policy.CredentialExpiresAt!.Value, policy.ObservedAt], token));
    }
}
