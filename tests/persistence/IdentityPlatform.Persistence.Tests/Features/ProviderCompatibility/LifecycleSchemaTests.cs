using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises additive populated upgrades and actual database lifecycle constraints on both supported providers.</summary>
public sealed class LifecycleSchemaTests
{
    private const long PriorRevision = 7;
    private const long PriorCredentialRevision = 3;
    private static readonly int[] ValidReasons = [1, 2, 3];
    private static readonly int[] InvalidReasons = [int.MinValue, -1, 0, 4, int.MaxValue];
    private const string Postgres = "PostgreSql";
    private const string Profile = "managed-web-v1";
    private const string AuditEvent = "ApplicationDisabled";
    private const string AuditActor = "Administrator";
    private const string AuditTarget = "Application";
    private const string Accepted = "Accepted";

    private const string Client = "synthetic-lifecycle-upgrade";
    private const int SecretBytes = 32;
    private const string LegacyPolicy = "INSERT INTO \"platform\".\"ApplicationPolicy\" (\"ApplicationId\", \"Profile\", \"Revision\", \"CredentialRevision\", \"CredentialIssuedAt\", \"CredentialExpiresAt\", \"ObservedAt\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})";

    /// <summary>Retains every old physical field/history entry and native credential through the real migrator, including a repeated latest upgrade.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedManagedPredecessor_When_LifecycleUpgradeRunsTwice_Then_ExistingDataAndCredentialRemainWithEnabledDefault(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            ManagedApplicationUpgradeWitness.PostgresMigration : ManagedApplicationUpgradeWitness.SqlMigration);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        await SeedAsync(fixture, secret);
        var before = await fixture.SnapshotAsync(provider);
        await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token);
        var after = GrantUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider));
        LifecycleUpgradeWitness.Require(provider, before, after);
        LifecycleUpgradeWitness.RejectDefects(provider, before, after);
        await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token);
        LifecycleUpgradeWitness.Require(provider, before, GrantUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider)));
        await using var scope = fixture.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(Client, fixture.Token);
        Assert.NotNull(application);
        Assert.True(await manager.ValidateClientSecretAsync(application, secret, fixture.Token));
        Assert.All(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().ToArrayAsync(fixture.Token), row => Assert.True(row.Enabled));
    }

    /// <summary>Seeds a real native registration plus explicit predecessor columns and phase/audit history without referencing the new EF property.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture fixture, string secret)
    {
        await using var scope = fixture.Scope();
        var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = Client,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential
        }, fixture.Token);
        var database = services.GetRequiredService<PlatformDbContext>();
        foreach (var id in new[] { await manager.GetIdAsync(application, fixture.Token), ManagedSchemaRows.ApplicationId })
        {
            await database.Database.ExecuteSqlRawAsync(LegacyPolicy, [id!, Profile, PriorRevision, PriorCredentialRevision,
                ManagedSchemaRows.Created, ManagedSchemaRows.Expires, ManagedSchemaRows.Expires + 1], fixture.Token);
        }
        foreach (var state in Enum.GetValues<ManagedLoginState>())
        {
            await ManagedLegacyRows.InsertAsync(database, ManagedSchemaRows.Interaction(state), fixture.Token);
        }
        await BrowserAuditMigrationRows.SeedAsync(database, services.GetRequiredService<IdentityPlatform.Infrastructure.Features.Sessions.Persistence.AdminSessionDbContext>(), fixture.Token);
    }

    /// <summary>Exercises every valid finite reason, invalid numeric codes, missing owner and false state on the actual relational schema.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LifecycleSchema_When_ReasonAndOwnershipConstraintsAreExercised_Then_OnlyOwnedFiniteReasonsAndExplicitDisabledStatePersist(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = ManagedSchemaRows.Policy();
        policy.Enabled = false;
        database.Add(policy);
        foreach (var code in ValidReasons)
        {
            var audit = Audit();
            database.Add(audit);
            database.Add(new ApplicationLifecycleAuditDetail { AuditId = audit.Id, ReasonCode = (ApplicationLifecycleReason)code });
        }
        await database.SaveChangesAsync(fixture.Token);
        database.ChangeTracker.Clear();
        Assert.False((await database.Set<ApplicationPolicy>().SingleAsync(fixture.Token)).Enabled);
        foreach (var code in InvalidReasons)
        {
            await RejectAsync(fixture, code, owner: true);
        }
        await RejectAsync(fixture, 1, owner: false);
        Assert.Equal(3, await database.Set<ApplicationLifecycleAuditDetail>().CountAsync(fixture.Token));
        Assert.Equal(3, await database.Set<SecurityAuditIntent>().CountAsync(fixture.Token));
    }

    /// <summary>Requires an actual failed database save and fresh-context rollback, not an application-only enum check.</summary>
    private static async Task RejectAsync(ManagedSchemaFixture fixture, int code, bool owner)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = Audit();
        if (owner)
        {
            database.Add(audit);
        }
        database.Add(new ApplicationLifecycleAuditDetail { AuditId = audit.Id, ReasonCode = (ApplicationLifecycleReason)code });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
    }

    /// <summary>Supplies bounded synthetic audit provenance solely for relational ownership checks.</summary>
    private static SecurityAuditIntent Audit() => new()
    {
        Event = AuditEvent,
        ActorType = AuditActor,
        TargetType = AuditTarget,
        Outcome = Accepted,
        OccurredAt = ManagedSchemaRows.Created
    };
}
