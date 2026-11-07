using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Proves populated immediate-predecessor upgrades preserve native and product state on both real engines.</summary>
public sealed class GrantUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string Client = "synthetic-grant-upgrade";
    private const string Subject = "synthetic-grant-subject";

    /// <summary>Runs the actual migrator twice and checks every prior physical value/history plus empty job additions.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedLifecyclePredecessor_When_GrantUpgradeRunsTwice_Then_AllPriorStateAndHistoryRemainExact(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            LifecycleUpgradeWitness.PostgresMigration : LifecycleUpgradeWitness.SqlMigration);
        await SeedAsync(fixture);
        var before = await fixture.SnapshotAsync(provider);
        await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token);
        var after = PilotGateUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider));
        GrantUpgradeWitness.Require(provider, before, after);
        GrantUpgradeWitness.RejectDefects(provider, before, after);
        await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token);
        GrantUpgradeWitness.Require(provider, before, PilotGateUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider)));
    }

    /// <summary>Seeds native authorization plus authorization-less token and disabled product metadata without any new job authority.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture fixture)
    {
        await using var scope = fixture.Scope();
        var services = scope.ServiceProvider;
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = Client,
            ClientType = OpenIddictConstants.ClientTypes.Public
        }, fixture.Token);
        var id = (await applications.GetIdAsync(application, fixture.Token))!;
        var policy = ManagedSchemaRows.Policy();
        policy.ApplicationId = id;
        policy.Enabled = false;
        var database = services.GetRequiredService<PlatformDbContext>();
        database.Add(policy);
        await database.SaveChangesAsync(fixture.Token);
        await services.GetRequiredService<IOpenIddictAuthorizationManager>().CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, fixture.Token);
        await services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
        }, fixture.Token);
    }
}
