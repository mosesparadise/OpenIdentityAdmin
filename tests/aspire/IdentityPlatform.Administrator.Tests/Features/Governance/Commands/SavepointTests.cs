using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Uses real provider configuration to require supported native savepoints and reject actual SQL MARS before attempting one.</summary>
[Collection("administrator-bootstrap")]
public sealed class SavepointTests(BootstrapFixture fixture)
{
    private const string NativeSupport = "native-savepoint-support";

    /// <summary>Enumerates a PostgreSQL supported control and a SQL Server MARS unsupported control without manufacturing PostgreSQL incapability.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> SupportRows => GovernanceRows.For(NativeSupport);

    /// <summary>Reads actual transaction support and verifies real factory behavior while all proof and coordination writes roll back.</summary>
    [Theory, MemberData(nameof(SupportRows))]
    public async Task Given_ActualProviderSavepointCapability_When_NativeGovernanceProofIsRequested_Then_OnlySupportedTransactionsVerify(string provider, string condition)
    {
        Assert.Equal(NativeSupport, condition);
        var observation = new ProofSavepointEvidence();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => observation.Configure(options, provider)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        var code = await scenario.UnusedCodeAsync();
        var before = await scenario.SnapshotAsync();
        var supported = provider == ProofSavepointEvidence.PostgreSql;
        await using (var operation = await GovernanceOperation.OpenAsync(scenario, request))
        {
            var native = operation.Get<PlatformDbContext>().Database.CurrentTransaction;
            Assert.NotNull(native);
            Assert.Equal(supported, native.SupportsSavepoints);
            observation.Begin();
            try
            {
                await RequireVerificationAsync(operation, scenario, code, supported);
            }
            finally { observation.Stop(); }
            observation.Require(supported);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Uses genuine password/TOTP verification when supported and requires unsupported providers to fail before native savepoint creation.</summary>
    private static async Task RequireVerificationAsync(GovernanceOperation operation, GovernanceScenario scenario, string code, bool supported)
    {
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (supported)
        {
            Assert.True((await operation.VerifyAsync(scenario, code)).Accepted);
        }
        else
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => operation.VerifyAsync(scenario, code));
        }
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
    }
}
