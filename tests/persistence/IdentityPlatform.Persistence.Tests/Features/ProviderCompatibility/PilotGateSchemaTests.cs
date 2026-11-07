using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks inert migration seeding, exact populated upgrades and runtime gate acquisition on both shipped engines.</summary>
public sealed class PilotGateSchemaTests
{
    private const int HistoricalRevision = 2;
    private const int HistoricalAuthorizations = 3;
    private const int HistoricalTokens = 4;
    private const string Postgres = "PostgreSql";
    private const string Client = "synthetic-pilot-gate-upgrade";

    /// <summary>Preserves every predecessor value and history across two actual Migrator executions and rejects damaged witness inputs.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedGrantPredecessor_When_PilotGateUpgradeRunsTwice_Then_OnlyInertCoordinationIsAdded(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            GrantUpgradeWitness.PostgresMigration : GrantUpgradeWitness.SqlMigration);
        await SeedAsync(fixture);
        await SeedGrantAsync(fixture);
        var before = await fixture.SnapshotAsync(provider);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        var after = await fixture.SnapshotAsync(provider);
        PilotGateUpgradeWitness.Require(provider, before, after);
        PilotGateUpgradeWitness.RejectDefects(provider, before, after);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        PilotGateUpgradeWitness.Require(provider, before, await fixture.SnapshotAsync(provider));
    }

    /// <summary>A fresh runtime principal can lock but cannot recreate a missing singleton; neither install nor request creates pilot authority.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_FreshUnbootstrappedSchema_When_RuntimeAcquiresThenGateIsMissing_Then_LockWorksAndMissingStateDenies(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        PilotGateUpgradeWitness.RequireSchema(provider, await fixture.SnapshotAsync(provider));
        await using var observer = fixture.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal("first-party-v1", (await database.Set<PilotProtocolGate>().AsNoTracking().SingleAsync(fixture.Token)).Id);
        Assert.Empty(await database.Set<PilotManifest>().AsNoTracking().ToListAsync(fixture.Token));
        var native = NativeProviderFixtures.Create(provider, database.Database.GetConnectionString()!);
        await native.PrepareRuntimePrincipals(fixture.Token);
        await using var runtime = new ServiceCollection().AddLogging().AddPlatformPersistence(native.Provider,
            native.RuntimeConnection(DatabaseStore.Platform), native.RuntimeConnection(DatabaseStore.Sessions)).BuildServiceProvider(validateScopes: true);
        var factory = runtime.GetRequiredService<IServiceScopeFactory>();
        var called = 0;
        await PilotProtocolTransaction.RunAsync(factory, TimeProvider.System, (_, _) =>
        {
            called++;
            return ValueTask.CompletedTask;
        }, fixture.Token);
        Assert.Equal(1, called);
        Assert.Equal(1, await database.Set<PilotProtocolGate>().ExecuteDeleteAsync(fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PilotProtocolTransaction.RunAsync(factory, TimeProvider.System, (_, _) =>
        {
            called++;
            return ValueTask.CompletedTask;
        }, fixture.Token).AsTask());
        Assert.Equal(1, called);
        Assert.Empty(await database.Set<PilotProtocolGate>().AsNoTracking().ToListAsync(fixture.Token));
        Assert.Empty(await database.Set<PilotManifest>().AsNoTracking().ToListAsync(fixture.Token));
    }

    /// <summary>Preserves populated approval, progress and audit provenance from the immediate predecessor schema.</summary>
    private static async Task SeedGrantAsync(ManagedSchemaFixture fixture)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = GrantSchemaRows.Add(database);
        rows.Job.State = GrantJobState.Processing;
        rows.Job.Revision = HistoricalRevision;
        rows.Job.AuthorizationCount = HistoricalAuthorizations;
        rows.Job.TokenCount = HistoricalTokens;
        rows.Detail.State = GrantJobState.Processing;
        rows.Detail.JobRevision = HistoricalRevision;
        rows.Detail.AuthorizationCount = HistoricalAuthorizations;
        rows.Detail.TokenCount = HistoricalTokens;
        await database.SaveChangesAsync(fixture.Token);
    }

    /// <summary>Uses native managers and disabled product policy to give the exact physical preservation witness meaningful predecessor rows.</summary>
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
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = Client,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, fixture.Token);
        await services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = await authorizations.GetIdAsync(authorization, fixture.Token),
            Subject = Client,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode
        }, fixture.Token);
    }
}
