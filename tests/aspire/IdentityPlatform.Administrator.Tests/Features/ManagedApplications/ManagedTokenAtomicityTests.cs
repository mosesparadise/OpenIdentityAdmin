using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Rejects completed native/final-save mutations and deadline changes inside the actual managed issuance owner.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenAtomicityTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Phases = [false, true];
    private static readonly ManagedHumanSaveDefect[] Defects = [ManagedHumanSaveDefect.InternalBinding, ManagedHumanSaveDefect.Ceremony,
        ManagedHumanSaveDefect.FinalBinding, ManagedHumanSaveDefect.Expired];

    /// <summary>Every injected database mutation actually completes before its snapshot rejects it; only earlier durable input consumption survives.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ActualManagedIssuance_When_SaveCallbackMutatesSourceOrExpiresProof_Then_AllIssuanceWritesRollBack(string provider, bool exchange, ManagedHumanSaveDefect defect)
    {
        var fault = new ManagedHumanSaveFault();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var expiry = Assert.Single((await test.SnapshotAsync()).Interactions).ExpiresAt;
        var before = await ManagedTokenEvidence.RollbackDigestAsync(test);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expiry);
        fault.Arm(defect);
        using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.True(fault.Fired && fault.MutationCompleted);
        Assert.Equal(before, await ManagedTokenEvidence.RollbackDigestAsync(test));
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }

    /// <summary>Identifies each independently completed mutation, native phase and real database in portable discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, ManagedHumanSaveDefect>> Cases => from provider in Providers
                                                                                            from exchange in Phases
                                                                                            from defect in Defects
                                                                                            select new TheoryDataRow<string, bool, ManagedHumanSaveDefect>(provider, exchange, defect).WithTrait(ProviderTrait, provider);
}
