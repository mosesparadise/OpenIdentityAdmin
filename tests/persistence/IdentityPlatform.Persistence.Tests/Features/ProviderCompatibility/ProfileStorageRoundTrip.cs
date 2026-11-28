using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks successful and refused schema downgrades with the actual provider and populated legacy history.</summary>
internal static class ProfileStorageRoundTrip
{
    private const string PostgreSql = "PostgreSql";
    private const string Native = "managed-native-v1";

    /// <summary>Restores the full retirement schema for legacy-only data and requires atomic refusal once a public policy exists.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture, string provider, Dictionary<string, string[]> upgraded)
    {
        var retirement = provider == PostgreSql ? RetirementUpgradeWitness.PostgresMigration : RetirementUpgradeWitness.SqlMigration;
        var expected = ProfileStorageWitness.Predecessor(provider, upgraded);
        await fixture.MigrateAsync(retirement);
        RequireEqual(expected, await fixture.SnapshotAsync(provider));
        await SeedRetirementAsync(fixture);
        var predecessor = await fixture.SnapshotAsync(provider);
        Assert.NotEmpty(predecessor[RetirementUpgradeWitness.Query]);
        await fixture.MigrateAsync(null);
        var restored = await fixture.SnapshotAsync(provider);
        RequireEqual(predecessor, ProfileStorageWitness.Predecessor(provider, restored));
        await fixture.MigrateAsync(null);
        RequireEqual(restored, await fixture.SnapshotAsync(provider));
        var profile = provider == PostgreSql ? ProfileStorageWitness.PostgresMigration : ProfileStorageWitness.SqlMigration;
        await fixture.MigrateAsync(profile);
        RequireEqual(BrowserOriginUpgradeTests.Predecessor(provider, restored), await fixture.SnapshotAsync(provider));
        await using (var scope = fixture.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Add(new ApplicationPolicy
            {
                ApplicationId = ManagedSchemaRows.Digest(),
                Profile = Native,
                Revision = 1,
                CredentialRevision = 0
            });
            await database.SaveChangesAsync(fixture.Token);
        }
        var withPublic = await fixture.SnapshotAsync(provider);
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.MigrateAsync(retirement));
        RequireEqual(withPublic, await fixture.SnapshotAsync(provider));
    }

    /// <summary>Binds the existing predecessor job, approval and audit to an already retired policy before upgrading.</summary>
    private static async Task SeedRetirementAsync(ManagedSchemaFixture fixture)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var approval = await database.Set<ApplicationGrantApproval>().SingleAsync(fixture.Token);
        var job = await database.Set<ApplicationGrantJob>().SingleAsync(fixture.Token);
        var policy = await database.Set<ApplicationPolicy>().SingleAsync(row => row.ApplicationId == job.ApplicationId, fixture.Token);
        var original = policy.Revision;
        policy.Revision++;
        policy.Retired = true;
        policy.Enabled = false;
        policy.ObservedAt = approval.ApprovedAt;
        approval.ApplicationRevision = policy.Revision;
        job.ApplicationRevision = policy.Revision;
        database.Add(new ApplicationRetirement
        {
            ApplicationId = policy.ApplicationId,
            ActorId = approval.ActorId,
            OriginalRevision = original,
            RetiredRevision = policy.Revision,
            Reason = approval.Reason,
            RetiredAt = approval.ApprovedAt,
            RequestKey = approval.RequestKey,
            ScopedKeyHash = approval.ScopedKeyHash,
            RequestHash = approval.RequestHash,
            ReplayUntil = approval.ReplayUntil,
            AuditId = approval.AuditId,
            GrantJobId = job.Id,
            GrantApprovalId = approval.Id
        });
        await database.SaveChangesAsync(fixture.Token);
    }

    /// <summary>Includes all physical metadata, every table row, and both migration histories without printing row contents.</summary>
    private static void RequireEqual(Dictionary<string, string[]> expected, Dictionary<string, string[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in expected)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(actual[query]));
        }
    }
}
