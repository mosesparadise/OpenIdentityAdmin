using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Requires additive upgrades to preserve data without manufacturing recovery authorization for legacy interactions.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryMigrationTests(BootstrapFixture fixture)
{
    private const string PostgreSql = "PostgreSql";
    private const string PreviousPostgres = "20261012204719_AdministratorJourneys";
    private const string PreviousSqlServer = "20261012204848_AdministratorJourneys";
    private const string RecoveryProof = "recovery-code";

    /// <summary>A retained pre-upgrade password interaction remains unauthorized and the database rejects partial proof tuples.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LegacyPasswordInteraction_When_RecoverySchemaUpgrades_Then_NoProofIsInferredAndPartialMetadataIsRejected(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Replacement, grant);
        var token = TestContext.Current.CancellationToken;
        await using var scope = scenario.Lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(provider == PostgreSql ? PreviousPostgres : PreviousSqlServer, token);
        await database.Database.MigrateAsync(token);
        var row = await database.Set<AdministratorInteraction>().SingleAsync(value => value.PublicId == initial, token);
        Assert.Null(row.AuthorizationKind);
        Assert.Null(row.AuthorizationVerifiedAt);
        Assert.Null(row.SourceFactorGeneration);
        Assert.Null(row.SessionPublicId);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(initial, scenario.Correlation, token,
            SecurityScenario.Replacement, SecurityScenario.Candidate(grant)));
        row.AuthorizationKind = RecoveryProof;
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
        const string PostgreSqlCheck = "23514";
        const int SqlServerCheck = 547;
        Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgreSqlCheck } ||
            failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerCheck });
        database.ChangeTracker.Clear();
        Assert.Null((await database.Set<AdministratorInteraction>().SingleAsync(value => value.PublicId == initial, token)).AuthorizationKind);
    }
}
