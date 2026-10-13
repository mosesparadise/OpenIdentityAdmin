using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Distinguishes rollback of native credential side effects, committed fifth-attempt lockout and noncredential capacity failure.</summary>
[Collection("administrator-bootstrap")]
public sealed class DenialTests(BootstrapFixture fixture)
{
    private const string Pruning = "savepoint-pruning-rollback";
    private const string Fifth = "fifth-failure-lockout";
    private const string Capacity = "full-replay-ledger";
    private const int EarlierFailures = 2;

    /// <summary>Enumerates one actual pruning/savepoint decision for each production database.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> PruningRows => GovernanceRows.For(Pruning);

    /// <summary>Enumerates one actual fifth-attempt transition for each production database.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> FifthRows => GovernanceRows.For(Fifth);

    /// <summary>Enumerates one full protected replay ledger for each production database.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> CapacityRows => GovernanceRows.For(Capacity);

    /// <summary>Requires a witnessed successful native deletion to be restored before committing only the ordinary failed-attempt effects.</summary>
    [Theory, MemberData(nameof(PruningRows))]
    public async Task Given_ExpiredReplayEntry_When_NativeFactorIsRejected_Then_SavepointRestoresPrunedMaterialAndCountsOnlyDenial(string provider, string condition)
    {
        Assert.Equal(Pruning, condition);
        var observation = new ProofReplayEvidence();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(observation)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        await ProofReplayState.ExpiredAsync(scenario);
        observation.Begin();
        try
        {
            await ProofDenialEvidence.RequireAsync(scenario, request, ProofCases.Totp);
        }
        finally { observation.Stop(); }
        observation.RequireSinglePrunedRow();
    }

    /// <summary>Requires the real fifth denial to persist both lockouts while preserving authority and making the old session unusable.</summary>
    [Theory, MemberData(nameof(FifthRows))]
    public async Task Given_FourEarlierFailures_When_FactorVerificationFails_Then_FifthDenialCommitsLockoutWithoutAuthorityChanges(string provider, string condition)
    {
        Assert.Equal(Fifth, condition);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        await ProofDenialEvidence.RequireFifthAsync(scenario, request);
    }

    /// <summary>Requires a valid independent code blocked by capacity to preserve the complete state and avoid a counted credential denial.</summary>
    [Theory, MemberData(nameof(CapacityRows))]
    public async Task Given_FullProtectedReplayLedger_When_ValidCredentialsArrive_Then_UnavailableRollsBackWithoutCountingFailure(string provider, string condition)
    {
        Assert.Equal(Capacity, condition);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        await ProofDenialEvidence.SeedFailuresAsync(scenario, EarlierFailures);
        var code = await ProofReplayState.FillAsync(scenario);
        var before = await scenario.SnapshotAsync();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(request, code: code));
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
