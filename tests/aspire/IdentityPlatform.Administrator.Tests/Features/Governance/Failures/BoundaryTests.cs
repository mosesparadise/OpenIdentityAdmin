using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Exercises actual protection failure and proof freshness at the native write boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class BoundaryTests(BootstrapFixture fixture)
{
    private const int ProofAgeSeconds = 300;
    private const string CriticalDecision = "CriticalDecision";
    private const string StaleProofDecision = "governance-stale-proof";
    private const string StaleProofBypass = "governance-stale-proof-bypass";
    private const string SafeFailure = "secret-safe-failure";
    private const string Expiry = "proof-expires";
    private const string Rollback = "clock-rollback";
    private const string DiagnosticControlFailure = "The secret-safe assertion control must reject without printing its synthetic canary.";
    private const string Canary = "synthetic-protected-material-canary-never-print";
    private const string SafeMessage = "Administrator governance completion is unavailable; reconcile stored state before further action.";
    public static IEnumerable<TheoryDataRow<string, string>> ProtectionCases => GovernanceRows.For(SafeFailure);
    public static IEnumerable<TheoryDataRow<string, string>> TimeCases => GovernanceRows.For(Expiry)
        .Select(row => row.WithTrait(CriticalDecision, StaleProofDecision)).Concat(GovernanceRows.For(Rollback));

    /// <summary>Corrupts protected fixture material and requires real unprotection failure to escape only through the fixed typed result.</summary>
    [Theory, MemberData(nameof(ProtectionCases))]
    public async Task Given_DependencyOrProtectionFailure_When_DecisionDenies_Then_OnlyTypedSafeOutcomeEscapes(string provider, string condition)
    {
        Assert.Equal(SafeFailure, condition);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        var code = await scenario.UnusedCodeAsync();
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<AdministratorFactor>().Where(row => row.UserId == scenario.ActorId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedSeed, Canary), TestContext.Current.CancellationToken));
        var before = await scenario.SnapshotAsync();
        var read = await Record.ExceptionAsync(() => scenario.ReadAsync());
        var command = await Record.ExceptionAsync(() => scenario.ExecuteAsync(request, code: code));
        RequireSafe(read);
        RequireSafe(command);
        var control = Record.Exception(() => RequireSafe(new InvalidOperationException(Canary)));
        Assert.True(control is Xunit.Sdk.XunitException && !control.ToString().Contains(Canary, StringComparison.Ordinal),
            DiagnosticControlFailure);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Uses only fixed boolean diagnostics even when a broken boundary returns raw credential-bearing exceptions.</summary>
    private static void RequireSafe(Exception? exception)
    {
        const string Failure = "The governance failure did not satisfy the fixed secret-safe contract; raw diagnostics are withheld.";
        Assert.True(exception?.GetType() == typeof(GovernanceUnavailableException) && exception.Message == SafeMessage &&
            exception.InnerException is null && exception.Data.Count == 0 && !exception.ToString().Contains(Canary, StringComparison.Ordinal), Failure);
    }

    /// <summary>Changes time after actual native password/TOTP proof and before the native target write, then requires complete rollback.</summary>
    [Theory, MemberData(nameof(TimeCases))]
    public async Task Given_InitiallyValidProof_When_TimeChangesBeforeProtectedWrite_Then_CommitDenies(string provider, string condition)
    {
        var boundary = new GovernanceTimeBoundary();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(boundary)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var replay = await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, TestContext.Current.CancellationToken);
        await RequireTimeBoundaryAsync(scenario, request, boundary, replay, condition);
    }

    /// <summary>Grants mutation credit only for independently verified durable acceptance after observed expiry, and always restores the test clock.</summary>
    private static async Task RequireTimeBoundaryAsync(GovernanceScenario scenario, GovernanceRequest request,
        GovernanceTimeBoundary boundary, int replay, string condition)
    {
        var before = await scenario.SnapshotAsync();
        var durable = await GovernanceDurableMutationEvidence.CaptureAsync(scenario);
        var now = scenario.Journey.Clock.Now;
        boundary.Arm(scenario, request.UserId!, request.RoleId!, replay, condition == Expiry);
        try
        {
            var outcome = GovernanceOutcome.Denied;
            var exception = await Record.ExceptionAsync(async () => outcome = await scenario.ExecuteAsync(request));
            Assert.True(boundary.Observed);
            if (condition == Expiry && outcome == GovernanceOutcome.Accepted)
            {
                Assert.Equal(now.AddSeconds(ProofAgeSeconds), scenario.Journey.Clock.Now);
                await GovernanceDurableMutationEvidence.RequireMembershipCommitAsync(scenario, request, durable);
                Assert.Fail(StaleProofBypass);
            }
            Assert.IsType<GovernanceUnavailableException>(exception);
            Assert.Equal(before, await scenario.SnapshotAsync());
            await scenario.Journey.Lease.AssertRuntimeDrainedAsync();
        }
        finally { scenario.Journey.Clock.Now = now; }
    }
}
