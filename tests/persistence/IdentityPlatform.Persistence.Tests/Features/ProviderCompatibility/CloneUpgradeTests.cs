using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Requires the clone audit migration to preserve populated native registrations, grants, policy and audit history on both engines.</summary>
public sealed class CloneUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string Client = "synthetic-clone-upgrade";
    private const string Subject = "synthetic-clone-subject";
    private const string Event = "ApplicationCreated";
    private const string Account = "account";
    private const string Application = "application";
    private const string Accepted = "accepted";

    /// <summary>Runs the actual Migrator twice from the immediate populated predecessor and calibrates the full physical-state witness.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedManagedScopePredecessor_When_CloneMigrationRunsTwice_Then_AllHistoryAndNativeStateRemainExact(string provider)
    {
        await using var test = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            ManagedScopeUpgradeWitness.PostgresMigration : ManagedScopeUpgradeWitness.SqlMigration);
        await SeedAsync(test);
        var before = await test.SnapshotAsync(provider);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        var after = await test.SnapshotAsync(provider);
        CloneUpgradeWitness.Require(provider, before, after);
        CloneUpgradeWitness.RejectDefects(provider, before, after);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        CloneUpgradeWitness.Require(provider, before, await test.SnapshotAsync(provider));
    }

    /// <summary>Populates native manager-owned grants plus product policy and existing audit history without inventing clone provenance.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture test)
    {
        await using var scope = test.Scope();
        var services = scope.ServiceProvider;
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = Client,
            ClientType = OpenIddictConstants.ClientTypes.Public
        }, test.Token);
        var id = (await applications.GetIdAsync(application, test.Token))!;
        var policy = ManagedSchemaRows.Policy();
        policy.ApplicationId = id;
        var database = services.GetRequiredService<PlatformDbContext>();
        await RetirementPredecessorRows.InsertAsync(database, policy, test.Token);
        database.Add(new SecurityAuditIntent
        {
            Event = Event,
            ActorType = Account,
            ActorId = Subject,
            TargetType = Application,
            TargetId = id,
            Outcome = Accepted,
            OccurredAt = ManagedSchemaRows.Created
        });
        await database.SaveChangesAsync(test.Token);
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, test.Token);
        await services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = await authorizations.GetIdAsync(authorization, test.Token),
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
        }, test.Token);
    }
}
