using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Exercises actual private native proofs against time, request, session and transaction ownership boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class ProofTests(BootstrapFixture fixture)
{
    private const int ProofAgeSeconds = 300;
    private const int InitialClockAdvanceSeconds = 2;
    private const int FutureOffsetSeconds = -1;

    /// <summary>Enumerates the exact ten C05 proof-equivalence rows on both providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> InvalidProofs => GovernanceRows.For(ProofCases.Invalid);

    /// <summary>Enumerates the two C11 lifetime/foreign-owner rows without attempting a second lock on the same singleton.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> ForeignOwners => GovernanceRows.For(ProofCases.Foreign, ProofCases.Ended);

    /// <summary>Enumerates the C12 native rehash precondition on both real providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> RehashRows => GovernanceRows.For(ProofCases.Rehash);

    /// <summary>Requires current native proof to deny changed boundaries and wrong credentials with only their explicitly permitted failure effects.</summary>
    [Theory, MemberData(nameof(InvalidProofs))]
    public async Task Given_CurrentSession_When_SensitiveProofIsInvalidOrRebound_Then_CommandDenies(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        if (condition is ProofCases.Password or ProofCases.Totp)
        {
            await ProofDenialEvidence.RequireAsync(scenario, request, condition);
            return;
        }
        var rebound = await ProofRequests.RebindAsync(scenario, request, condition);
        var otherSession = condition == ProofCases.Session ? await ProofSessions.OtherAsync(scenario) : null;
        scenario.Journey.Clock.Now = scenario.Journey.Clock.Now.AddSeconds(InitialClockAdvanceSeconds);
        var before = await scenario.SnapshotAsync();
        await using (var owner = await ProofOwner.OpenAsync(scenario, request))
        {
            await RequireBoundaryAsync(owner, scenario, condition, rebound, otherSession);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Rejects a genuinely verified capability under a second live native owner or after the first owner has ended, without leaked writes.</summary>
    [Theory, MemberData(nameof(ForeignOwners))]
    public async Task Given_RealTransactionOwnedProof_When_ReusedInAnotherTransaction_Then_ConsumptionDenies(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await using var foreign = condition == ProofCases.Foreign ? await GovernanceScenario.CreateAsync(fixture, provider) : null;
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        var otherRequest = foreign is null ? null : await GovernanceCommands.PrepareAsync(foreign, GovernanceCommands.RestoreGrant);
        var before = await scenario.SnapshotAsync();
        var otherBefore = foreign is null ? null : await foreign.SnapshotAsync();
        await using (var owner = await ProofOwner.OpenAsync(scenario, request))
        {
            if (foreign is not null)
            {
                await using var other = await GovernanceOperation.OpenAsync(foreign, otherRequest!);
                Assert.Throws<FactorUnavailableException>(() => owner.Proof.Consume(other.Transaction, owner.Verification.Actor, owner.Verification.VerifiedAt));
                owner.Proof.Consume(owner.Operation.Transaction, owner.Verification.Actor, owner.Verification.VerifiedAt);
                owner.Proof.RequireConsumed(owner.Operation.Transaction, request, owner.Verification.VerifiedAt);
            }
            else
            {
                Assert.Equal(ProofCases.Ended, condition);
                await owner.DisposeAsync();
                Assert.Throws<FactorUnavailableException>(() => owner.Proof.Consume(owner.Operation.Transaction, owner.Verification.Actor, owner.Verification.VerifiedAt));
            }
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
        if (foreign is not null)
        {
            Assert.Equal(otherBefore, await foreign.SnapshotAsync());
        }
    }

    /// <summary>Requires the native rehash write to invalidate the original session and roll back instead of binding old authority to new credentials.</summary>
    [Theory, MemberData(nameof(RehashRows))]
    public async Task Given_NativePasswordRequiresRehash_When_OriginalSessionNoLongerValidates_Then_CommandRollsBackWithoutRebinding(string provider, string condition)
    {
        Assert.Equal(ProofCases.Rehash, condition);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await ProofRehash.RequireAsync(scenario);
    }

    /// <summary>Uses original-request controls before testing consumed, altered intent, tracked-session substitution or strict time rejection.</summary>
    private static async Task RequireBoundaryAsync(ProofOwner owner, GovernanceScenario scenario, string condition,
        GovernanceRequest? rebound, string? otherSession)
    {
        var transaction = owner.Operation.Transaction;
        var actor = owner.Verification.Actor;
        var now = owner.Verification.VerifiedAt;
        if (rebound is not null || condition == ProofCases.Consumed)
        {
            owner.Proof.Consume(transaction, actor, now);
            owner.Proof.RequireConsumed(transaction, owner.Proof.Request, now);
            if (rebound is not null)
            {
                Assert.Throws<FactorUnavailableException>(() => owner.Proof.RequireConsumed(transaction, rebound, now));
            }
            else
            {
                Assert.Throws<FactorUnavailableException>(() => owner.Proof.Consume(transaction, actor, now));
            }
            return;
        }
        if (condition == ProofCases.Session)
        {
            await ProofSessions.RebindTrackedAsync(owner, scenario, otherSession!);
        }
        else
        {
            Assert.True(condition is ProofCases.Aged or ProofCases.Future);
            now += condition == ProofCases.Aged ? ProofAgeSeconds : FutureOffsetSeconds;
        }
        Assert.Throws<FactorUnavailableException>(() => owner.Proof.Consume(transaction, actor, now));
    }
}
