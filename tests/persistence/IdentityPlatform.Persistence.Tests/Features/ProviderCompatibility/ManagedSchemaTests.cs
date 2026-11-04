using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Proves additive upgrade, finite state constraints and portable optimistic concurrency against both actual relational engines.</summary>
public sealed class ManagedSchemaTests
{
    private const string ExistingClient = "synthetic-preexisting-client";
    private const string ExistingUser = "synthetic-preexisting-user";
    private const int SecretBytes = 32;
    private const long SecondRevision = 2;

    /// <summary>Populates real native rows at the immediate previous schema, then compares every old physical value through upgrade and repeat.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedImmediatePriorSchema_When_ManagedTablesAreAdded_Then_AllOldRowsAndHistoryRemainWithoutAutomaticAdoption(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, prior: true);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        await SeedAsync(fixture, secret);
        var before = await fixture.SnapshotAsync(provider);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        var after = await fixture.SnapshotAsync(provider);
        ManagedApplicationUpgradeWitness.Require(provider, before, after);
        ManagedApplicationUpgradeWitness.RejectDefects(provider, before, after);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        ManagedApplicationUpgradeWitness.Require(provider, before, await fixture.SnapshotAsync(provider));
        await using var scope = fixture.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(ExistingClient, fixture.Token);
        Assert.NotNull(application);
        Assert.True(await manager.ValidateClientSecretAsync(application, secret, fixture.Token));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().ToArrayAsync(fixture.Token));
    }

    /// <summary>Uses native managers for preexisting credentials and account rows; schema snapshots compare their complete stored representations without printing values.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture fixture, string secret)
    {
        await using var scope = fixture.Scope();
        var services = scope.ServiceProvider;
        var user = new PlatformUser { UserName = ExistingUser };
        Assert.True((await services.GetRequiredService<UserManager<PlatformUser>>().CreateAsync(user)).Succeeded);
        await services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ExistingClient,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential
        }, fixture.Token);
        await BrowserAuditMigrationRows.SeedAsync(services.GetRequiredService<PlatformDbContext>(),
            services.GetRequiredService<IdentityPlatform.Infrastructure.Features.Sessions.Persistence.AdminSessionDbContext>(), fixture.Token);
    }

    /// <summary>Expired lifecycle metadata and every valid static phase roundtrip while malformed times, revisions, proofs and duplicate native bindings fail.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedSchema_When_PhaseBoundsAndBindingsAreExercised_Then_OnlyValidPortableRowsPersist(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(ManagedSchemaRows.Policy());
        database.AddRange(ManagedSchemaRows.Reservation(), ManagedSchemaRows.Reservation());
        database.Add(ManagedSchemaRows.Boundaries());
        foreach (var state in Enum.GetValues<ManagedLoginState>())
        {
            database.Add(ManagedSchemaRows.Interaction(state));
        }
        await database.SaveChangesAsync(fixture.Token);
        database.ChangeTracker.Clear();
        var policy = await database.Set<ApplicationPolicy>().SingleAsync(fixture.Token);
        Assert.True(policy.ObservedAt > policy.CredentialExpiresAt);
        ManagedSchemaColumns.Require(provider, (await fixture.SnapshotAsync(provider))[DatabaseSnapshot.Columns]);
        await ManagedSchemaNegatives.RequireAsync(fixture);
    }

    /// <summary>Stale aggregate and clock-only writers fail on both engines; observing a clock never silently changes the public aggregate version.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoTrackedWriters_When_AggregateOrWatermarkChanges_Then_StalePersistenceFailsWithoutRenewingVersion(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var first = fixture.Scope();
        await using var second = fixture.Scope();
        var left = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var right = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        left.Add(ManagedSchemaRows.Policy());
        await left.SaveChangesAsync(fixture.Token);
        var winner = await left.Set<ApplicationPolicy>().SingleAsync(fixture.Token);
        var stale = await right.Set<ApplicationPolicy>().SingleAsync(fixture.Token);
        winner.Revision = SecondRevision;
        await left.SaveChangesAsync(fixture.Token);
        stale.Revision = SecondRevision;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync(fixture.Token));
        right.ChangeTracker.Clear();
        stale = await right.Set<ApplicationPolicy>().SingleAsync(fixture.Token);
        winner.ObservedAt++;
        await left.SaveChangesAsync(fixture.Token);
        stale.ObservedAt++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync(fixture.Token));
        right.ChangeTracker.Clear();
        var current = await right.Set<ApplicationPolicy>().SingleAsync(fixture.Token);
        Assert.Equal(SecondRevision, current.Revision);
        Assert.Equal(1, current.CredentialRevision);
        Assert.Equal(ManagedSchemaRows.Expires + SecondRevision, current.ObservedAt);
    }

    /// <summary>Proves interaction revision and observation guards independently reject stale writers without extending the original deadline.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoInteractionWriters_When_RevisionOrObservationChanges_Then_StaleWritesFailAndDeadlineRemains(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var first = fixture.Scope();
        await using var second = fixture.Scope();
        var left = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var right = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        left.Add(ManagedSchemaRows.Interaction(ManagedLoginState.PendingPassword));
        await left.SaveChangesAsync(fixture.Token);
        var winner = await left.Set<ManagedLoginInteraction>().SingleAsync(fixture.Token);
        var stale = await right.Set<ManagedLoginInteraction>().SingleAsync(fixture.Token);
        winner.Revision = SecondRevision;
        await left.SaveChangesAsync(fixture.Token);
        stale.State = ManagedLoginState.Denied;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync(fixture.Token));
        right.ChangeTracker.Clear();
        stale = await right.Set<ManagedLoginInteraction>().SingleAsync(fixture.Token);
        winner.ObservedAt++;
        await left.SaveChangesAsync(fixture.Token);
        stale.State = ManagedLoginState.Expired;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync(fixture.Token));
        right.ChangeTracker.Clear();
        var current = await right.Set<ManagedLoginInteraction>().SingleAsync(fixture.Token);
        Assert.Equal(SecondRevision, current.Revision);
        Assert.Equal(ManagedSchemaRows.Created + 1, current.ObservedAt);
        Assert.Equal(ManagedLoginState.PendingPassword, current.State);
        Assert.Equal(ManagedSchemaRows.Expires, current.ExpiresAt);
    }
}
