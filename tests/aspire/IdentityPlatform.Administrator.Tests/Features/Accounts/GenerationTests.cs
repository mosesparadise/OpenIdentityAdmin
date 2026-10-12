using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Verifies upgrade continuity and checked allocation independently of new-database schema creation.</summary>
[Collection("administrator-bootstrap")]
public sealed class GenerationTests(BootstrapFixture fixture)
{
    private const string PostgreSql = "PostgreSql";
    private const string PreviousPostgres = "20261012180735_AdministratorFactors";
    private const string PreviousSqlServer = "20261012180753_AdministratorFactors";
    private const long LargestGeneration = 17;
    private const string Enrollment = "Enrollment";

    /// <summary>Every retained generation source independently establishes the upgrade high-water mark, while legacy interactions remain ineligible.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PreviousSchemaWithRetainedFactors_When_JourneySchemaUpgrades_Then_AllGenerationSourcesAndLegacyDenialArePreserved(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        await using var scope = scenario.Lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var userId = await database.Users.Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
        database.Add(new AdministratorFactor { UserId = userId });
        database.Add(new AdministratorReplay { UserId = userId, AcceptedAt = 0, ExpiresAt = 1 });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Action<AdministratorEnrollment, AdministratorFactor, AdministratorInteraction, AdministratorReplay>[] maxima =
        [
            (enrollment, _, _, _) => enrollment.FactorGeneration = LargestGeneration,
            (_, factor, _, _) => factor.Generation = LargestGeneration,
            (_, _, interaction, _) => interaction.DestinationGeneration = LargestGeneration,
            (_, _, _, replay) => replay.Generation = LargestGeneration
        ];
        foreach (var maximum in maxima)
        {
            await RequireBackfillAsync(database, provider, maximum);
        }
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, TestContext.Current.CancellationToken));
    }

    /// <summary>Counter exhaustion cannot wrap into a reused destination or leave a partially admitted interaction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExhaustedGenerationAllocator_When_EnrollmentIsAdmitted_Then_OverflowRollsBackWithoutReusingDestination(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var state = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        state.LastAllocatedFactorGeneration = long.MaxValue;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<OverflowException>(() => scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username,
            scenario.Lease.Password, Enrollment, scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken));
        Assert.Empty(await scenario.Database.Set<AdministratorInteraction>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(long.MaxValue, (await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken)).LastAllocatedFactorGeneration);
    }

    /// <summary>Downgrades a real schema, then upgrades with exactly one largest independent retained generation.</summary>
    private static async Task RequireBackfillAsync(PlatformDbContext database, string provider,
        Action<AdministratorEnrollment, AdministratorFactor, AdministratorInteraction, AdministratorReplay> maximum)
    {
        var token = TestContext.Current.CancellationToken;
        database.ChangeTracker.Clear();
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(token);
        var factor = await database.Set<AdministratorFactor>().SingleAsync(token);
        var interaction = await database.Set<AdministratorInteraction>().SingleAsync(token);
        var replay = await database.Set<AdministratorReplay>().SingleAsync(token);
        enrollment.FactorGeneration = 0;
        factor.Generation = 1;
        interaction.DestinationGeneration = 1;
        replay.Generation = 1;
        maximum(enrollment, factor, interaction, replay);
        await database.SaveChangesAsync(token);
        await database.GetService<IMigrator>().MigrateAsync(provider == PostgreSql ? PreviousPostgres : PreviousSqlServer, token);
        await database.Database.MigrateAsync(token);
        database.ChangeTracker.Clear();
        Assert.Equal(LargestGeneration, (await database.Set<AdministratorAuthentication>().SingleAsync(token)).LastAllocatedFactorGeneration);
        Assert.Null((await database.Set<AdministratorInteraction>().SingleAsync(token)).ExpectedEnrollmentRevision);
    }
}
