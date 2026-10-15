using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Proves both witness-security orderings and a decisive post-observation omitted-lock commit boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class WitnessOrderingTests(BootstrapFixture fixture)
{
    private const string AuthenticationTable = "AdministratorAuthentication";
    private const string Update = "UPDATE";
    private const string CriticalDecision = "CriticalDecision";
    private const string WitnessDecision = "omitted-witness-lock";
    private const string UnsafeCommit = "governance-witness-lock-bypass";
    private const int OneWriter = 1;

    /// <summary>Distinguishes genuine lockout/recovery from the three explicitly scoped corruption controls.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> PriorDamage => GovernanceRows.For(WitnessSecurity.Lockout,
        WitnessSecurity.Recovery, WitnessSecurity.Suspension, WitnessSecurity.Disable, WitnessSecurity.FactorChange);
    /// <summary>Requires both mandatory production events to remain available after safe committed governance.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Mandatory => GovernanceRows.For(WitnessSecurity.Lockout, WitnessSecurity.RestrictedRecovery);
    /// <summary>Designates one independent actual-commit mutation oracle per supported database.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => StoreTests.Providers.Select(row => row.WithTrait(CriticalDecision, WitnessDecision));

    /// <summary>Holds a genuine witness update, observes the governing mutation waiting, and then requires fresh denial after damage commits.</summary>
    [Theory, MemberData(nameof(PriorDamage))]
    public async Task Given_LockedWitness_When_SecurityStateCommitsBeforeWaitingMutation_Then_WitnessIsRechecked(string provider, string variant)
    {
        var gate = new DatabaseCommandGate();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate)));
        var witness = await WitnessSecurity.CreateAsync(scenario);
        var security = await witness.PrepareAsync(scenario, variant);
        var request = await WitnessEvidence.RequestAsync(scenario);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task? write = null, observation = null;
        Task<GovernanceOutcome>? governance = null;
        try
        {
            gate.Arm(AuthenticationTable, Update);
            write = security(lifetime.Token);
            var blocker = await gate.WaitAsync(write, lifetime.Token);
            governance = WitnessEvidence.ExecuteAsync(scenario, request, lifetime.Token);
            var stage = variant == WitnessSecurity.Recovery ? WitnessEvidence.GovernanceStage : WitnessEvidence.AccountStage;
            observation = DatabaseWaiters.ObserveOwnedAsync(scenario.Journey.Lease, blocker, stage, OneWriter, lifetime.Token);
            Assert.Same(observation, await Task.WhenAny(observation, governance, write));
            await observation;
            gate.Release();
            await write;
            await Assert.ThrowsAsync<GovernanceUnavailableException>(() => governance);
        }
        finally { gate.Release(); await lifetime.CancelAsync(); await WitnessEvidence.DrainAsync(write, governance, observation); }
        await WitnessEvidence.RequireSecurityAsync(scenario, witness, variant);
        await WitnessEvidence.RequireAuthorityAsync(scenario, request, accepted: false);
        if (variant == WitnessSecurity.FactorChange)
        {
            await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ReadAsync());
        }
        else
        {
            Assert.Equal(scenario.ActorId, Assert.Single((await scenario.ReadAsync()).UsableAdministrators));
        }
    }

    /// <summary>Accepts safe self-demotion and then proves mandatory native security enforcement may leave no usable administrator.</summary>
    [Theory, MemberData(nameof(Mandatory))]
    public async Task Given_SafeCommittedGovernance_When_MandatorySecurityEventFollows_Then_UnsafeAuthorityIsNotPreserved(string provider, string variant)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var witness = await WitnessSecurity.CreateAsync(scenario);
        var security = await witness.PrepareAsync(scenario, variant);
        var request = await WitnessEvidence.RequestAsync(scenario);
        Assert.Equal(GovernanceOutcome.Accepted, await scenario.ExecuteAsync(request));
        Assert.Equal(witness.Id, Assert.Single((await scenario.ReadAsync()).UsableAdministrators));
        await security(TestContext.Current.CancellationToken);
        await WitnessEvidence.RequireSecurityAsync(scenario, witness, variant);
        await WitnessEvidence.RequireAuthorityAsync(scenario, request, accepted: true);
        Assert.Empty((await scenario.ReadAsync()).UsableAdministrators);
    }

    /// <summary>Kills only an actual unsafe accepted commit after the final witness read; a waiter timeout or unavailable operation is not decisive evidence.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_FinalWitnessObservation_When_MandatoryLockoutRacesNativeCommit_Then_WitnessLockSerializesTheCommit(string provider)
    {
        WitnessDiagnostics.Write(WitnessStage.Setup);
        var gate = new NativeCommitGate();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate)));
        var prepared = await WitnessEvidence.PrepareCommitRaceAsync(scenario);
        await using var scope = scenario.CreateScope();
        gate.Arm(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task? write = null, observation = null;
        Task<GovernanceOutcome>? governance = null;
        var unsafeOrder = false;
        WitnessDiagnostics.Write(WitnessStage.Prepared);
        try
        {
            governance = scope.ServiceProvider.GetRequiredService<GovernanceService>().ExecuteAsync(prepared.Request, scenario.Session,
                scenario.Password, prepared.Code, lifetime.Token);
            WitnessDiagnostics.Write(WitnessStage.GateWait);
            var blocker = await gate.WaitAsync(governance, lifetime.Token);
            WitnessDiagnostics.Write(WitnessStage.GateArrived);
            write = prepared.Security(lifetime.Token);
            observation = DatabaseWaiters.ObserveOwnedAsync(scenario.Journey.Lease, blocker, WitnessEvidence.AccountStage, OneWriter, lifetime.Token);
            WitnessDiagnostics.Write(WitnessStage.OrderWait);
            unsafeOrder = await WitnessEvidence.RequireOrderAsync(scenario, prepared.Witness, gate, write, governance, observation);
            WitnessDiagnostics.Write(WitnessStage.OrderComplete);
            gate.Release();
            WitnessDiagnostics.Write(WitnessStage.CommitResult);
            Assert.Equal(GovernanceOutcome.Accepted, await governance);
            await write;
            WitnessDiagnostics.Write(WitnessStage.WriterJoined);
        }
        finally { WitnessDiagnostics.Write(WitnessStage.Drain); gate.Release(); await lifetime.CancelAsync(); await WitnessEvidence.DrainAsync(write, governance, observation); }
        await WitnessEvidence.RequireCommittedLockoutAsync(scenario, prepared.Witness, prepared.Request);
        WitnessDiagnostics.Write(WitnessStage.Oracle);
        Assert.False(unsafeOrder, UnsafeCommit);
    }
}
