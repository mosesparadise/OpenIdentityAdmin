using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises the retained device cadence, pinned key revision and cross-revision assertion uniqueness on the actual provider.</summary>
public sealed class DeviceKeyUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string Sql = "SqlServer";
    private const string Provider = "Provider";
    private const string Device = "managed-device-v1";
    private const string Key = "managed-machine-key-v1";
    private const string Schema = "platform";
    private const string First = "first";
    private const string Second = "second";
    private const string NativeEntry = "synthetic-native-token-entry";
    private const string Jti = "unique_assertion_id";
    private const int Bits = 3072;
    private const long Start = 1000;
    private const long Deadline = 1600;
    private const long SecondRevision = 2;
    private const long Slow = 1002;
    private const long OnTime = 1012;
    private const long AssertionExpiry = 1050;
    private static readonly string[] Tables = ["DevicePollingWindow", "ApplicationKeyApproval", "ApplicationAssertionUse"];
    internal const string PgMigration = "20261128003643_ManagedDevicePollingAndClientKeys";
    internal const string SqlMigration = "20261128003647_ManagedDevicePollingAndClientKeys";
    internal const string PostgresKeyAudit = "20261128013521_ManagedKeyAuditRevision";
    internal const string SqlKeyAudit = "20261128013526_ManagedKeyAuditRevision";
    private const string KeyAuditTable = "ApplicationKeyAuditDetail";
    private const string Read = "SELECT * FROM";
    private const string TablePrefix = "SELECT * FROM \"platform\".\"";

    /// <summary>Verifies a clean empty round trip and prevents a populated downgrade after real provider state transitions.</summary>
    [Theory]
    [InlineData(Postgres, Traits = [Provider, Postgres])]
    [InlineData(Sql, Traits = [Provider, Sql])]
    public async Task Given_DeviceAndKeyState_When_UpgradingAndRotating_Then_TheCadenceAndUsedAssertionSurvive(string provider)
    {
        var previous = provider == Postgres ? BrowserOriginUpgradeTests.PostgresMigration : BrowserOriginUpgradeTests.SqlMigration;
        var protocolClock = new NativeConsumptionClock();
        protocolClock.Freeze();
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: previous, protocolClock: protocolClock, configure: services =>
        {
            NativeDevicePollEvidence.Configure(services);
            ManagedKeyAssertionEvidence.Configure(services);
        });
        var before = await fixture.SnapshotAsync(provider);
        await fixture.MigrateAsync(null);
        RequireEqual(before, Predecessor(provider, await fixture.SnapshotAsync(provider)));
        await fixture.MigrateAsync(previous);
        RequireEqual(before, await fixture.SnapshotAsync(provider));
        await fixture.MigrateAsync(null);
        await RequirePollingAsync(fixture);
        await RequireKeysAsync(fixture);
        await ManagedKeyAssertionEvidence.RequireAsync(fixture);
        await NativeDevicePollEvidence.RequireAsync(fixture);
        var bound = await fixture.SnapshotAsync(provider);
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.MigrateAsync(provider == Postgres ? PgMigration : SqlMigration));
        RequireEqual(bound, await fixture.SnapshotAsync(provider));
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.MigrateAsync(previous));
        await fixture.MigrateAsync(null);
        RequireEqual(bound, await fixture.SnapshotAsync(provider));
    }

    /// <summary>Checks persisted backoff including competing poll writers, exact interval equality and ten-minute expiry.</summary>
    private static async Task RequirePollingAsync(ManagedSchemaFixture fixture)
    {
        await using var first = fixture.Scope();
        var database = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = ManagedSchemaRows.Digest();
        var policy = new ApplicationPolicy { ApplicationId = id, Profile = Device, Revision = 1 };
        database.Add(policy);
        var window = DevicePollingWindow.Start(NativeEntry, policy, Start);
        database.Add(window);
        await database.SaveChangesAsync(fixture.Token);
        await using var second = fixture.Scope();
        var other = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stale = await other.Set<DevicePollingWindow>().SingleAsync(fixture.Token);
        Assert.Equal(DevicePollResult.Unavailable, window.Poll(new ApplicationPolicy { ApplicationId = id, Profile = Device, Revision = SecondRevision }, Slow));
        Assert.Equal(DevicePollResult.SlowDown, window.Poll(policy, Slow));
        await database.SaveChangesAsync(fixture.Token);
        Assert.Equal(DevicePollResult.SlowDown, stale.Poll(policy, Slow));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync(fixture.Token));
        await other.Entry(stale).ReloadAsync(fixture.Token);
        Assert.Equal(OnTime, stale.NextPollAt);
        Assert.Equal(DevicePollResult.Unavailable, stale.Poll(policy, Start));
        Assert.Equal(DevicePollResult.Admitted, stale.Poll(policy, OnTime));
        await other.SaveChangesAsync(fixture.Token);
        Assert.Equal(DevicePollResult.Unavailable, stale.Poll(policy, Deadline));
    }

    /// <summary>Exercises actual RSA public metadata and native key thumbprints with a duplicate assertion after replacement.</summary>
    private static async Task RequireKeysAsync(ManagedSchemaFixture fixture)
    {
        await using var first = fixture.Scope();
        var database = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = new ApplicationPolicy
        {
            ApplicationId = ManagedSchemaRows.Digest(),
            Profile = Key,
            Revision = 1,
            CredentialRevision = 1,
            CredentialIssuedAt = Start,
            CredentialExpiresAt = Deadline,
            ObservedAt = Start
        };
        var original = PublicKey(First);
        var replacement = PublicKey(Second);
        var approval = ApplicationKeyApproval.Create(policy, original);
        database.Add(policy);
        database.Add(approval);
        Assert.Null(ApplicationAssertionUse.FromValidated(policy, approval, original, replacement.CreateNativeKey(), Jti, Start, AssertionExpiry, null, Start));
        database.Add(Use(policy, approval, original));
        await database.SaveChangesAsync(fixture.Token);
        policy.Revision = policy.CredentialRevision = SecondRevision;
        RequireDifferentMaterial(policy, approval, original);
        approval.Replace(policy, replacement);
        Assert.False(approval.Matches(policy, original, original.CreateNativeKey(), Start));
        Assert.True(approval.Matches(policy, replacement, replacement.CreateNativeKey(), Start));
        Assert.False(approval.Matches(policy, replacement, replacement.CreateNativeKey(), Deadline));
        await database.SaveChangesAsync(fixture.Token);
        await using var second = fixture.Scope();
        var other = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        other.Add(Use(policy, approval, replacement));
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(fixture.Token));
    }

    /// <summary>Re-labeling the same public RSA key cannot advance the credential revision.</summary>
    private static void RequireDifferentMaterial(ApplicationPolicy policy, ApplicationKeyApproval approval, AdministratorPublicKey original)
    {
        var alias = original.CreateNativeKey();
        alias.Kid = Second;
        var relabeled = AdministratorPublicKey.Parse(JsonSerializer.Serialize(new
        {
            kty = alias.Kty,
            kid = alias.Kid,
            alg = alias.Alg,
            use = alias.Use,
            n = alias.N,
            e = alias.E
        }));
        Assert.Throws<InvalidOperationException>(() => approval.Replace(policy, relabeled!));
    }

    /// <summary>Creates replay state using the synthetic matching public key without exposing private RSA parameters.</summary>
    private static ApplicationAssertionUse Use(ApplicationPolicy policy, ApplicationKeyApproval approval, AdministratorPublicKey key) =>
        ApplicationAssertionUse.FromValidated(policy, approval, key, key.CreateNativeKey(), Jti, Start, AssertionExpiry, null, Start)!;

    /// <summary>Generates a fresh public-only JWK using the native RSA implementation for each synthetic revision.</summary>
    private static AdministratorPublicKey PublicKey(string id)
    {
        using var rsa = RSA.Create(Bits);
        var key = rsa.ExportParameters(false);
        const string rsaType = "RSA";
        const string use = "sig";
        return AdministratorPublicKey.Parse(JsonSerializer.Serialize(new
        {
            kty = rsaType,
            kid = id,
            alg = SecurityAlgorithms.RsaSha256,
            use,
            n = Base64UrlEncoder.Encode(key.Modulus!),
            e = Base64UrlEncoder.Encode(key.Exponent!)
        }))!;
    }

    /// <summary>Tests whether the latest relation exists before projecting a predecessor snapshot.</summary>
    internal static bool Present(Dictionary<string, string[]> source) => source.ContainsKey(TablePrefix + Tables[0] + '"');

    /// <summary>Removes only the three known empty tables and the one actual history row from a current snapshot.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> source)
    {
        if (source.ContainsKey(TablePrefix + KeyAuditTable + '"'))
        {
            source = RemoveMigration(source, provider == Postgres ? PostgresKeyAudit : SqlKeyAudit, [KeyAuditTable]);
        }
        return RemoveMigration(source, provider == Postgres ? PgMigration : SqlMigration, Tables);
    }

    /// <summary>Checks exact history growth and requires every removed table to be empty before comparing earlier rows.</summary>
    private static Dictionary<string, string[]> RemoveMigration(Dictionary<string, string[]> source, string migration, string[] tables)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (query, rows) in source)
        {
            if (tables.Any(table => query == TablePrefix + table + '"'))
            {
                Assert.Empty(rows);
                continue;
            }
            result[query] = query == AdministratorUpgradeWitness.PlatformHistory ? rows.Where(row => Parts(row)[0] != migration).ToArray() :
                query.StartsWith(Read, StringComparison.Ordinal) ? rows : rows.Where(row => Parts(row) is not [Schema, var name, ..] || !tables.Contains(name)).ToArray();
        }
        BrowserAuditMigrationRows.RequireHistory(result[AdministratorUpgradeWitness.PlatformHistory], source[AdministratorUpgradeWitness.PlatformHistory], migration);
        return result;
    }

    /// <summary>Compares every captured predecessor schema and data fingerprint.</summary>
    private static void RequireEqual(Dictionary<string, string[]> expected, Dictionary<string, string[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in expected)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(actual[query]));
        }
    }

    /// <summary>Reads independently captured physical metadata.</summary>
    private static string?[] Parts(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
