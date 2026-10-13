using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Requires native manager cancellation and original context ownership without fabricated private capabilities.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeOwnershipTests(BootstrapFixture fixture)
{
    private const string RoleCancelled = "role-manager-cancelled";
    private const string UserCancelled = "user-manager-cancelled";
    private const string Foreign = "foreign-context-owner";
    private const string Ended = "ended-transaction-owner";
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(RoleCancelled, UserCancelled, Foreign, Ended);

    /// <summary>Actual provider waits cancel under their original token; foreign or ended owners deny before native statements escape.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_NativeGovernanceManager_When_CancelledOrDetached_Then_TransactionOwnershipCannotEscape(string provider, string variant)
    {
        var evidence = new GovernanceCancellationEvidence();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(evidence)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        var before = await scenario.SnapshotAsync();
        switch (variant)
        {
            case RoleCancelled:
            case UserCancelled:
                await GovernanceNativeCancellation.RequireAsync(scenario, evidence, provider, variant == RoleCancelled);
                break;
            case Foreign:
                await GovernanceManagerOwnership.RequireForeignAsync(scenario, evidence);
                break;
            case Ended:
                await GovernanceManagerOwnership.RequireEndedAsync(scenario, evidence);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
        await scenario.Journey.Lease.AssertRuntimeDrainedAsync();
        if (variant is RoleCancelled or UserCancelled)
        {
            await using var native = await GovernanceNativeBlock.AcquireAsync(scenario.Journey.Lease, variant == RoleCancelled);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
        await using var reacquired = await GovernanceOperation.OpenAsync(scenario, request);
    }
}
