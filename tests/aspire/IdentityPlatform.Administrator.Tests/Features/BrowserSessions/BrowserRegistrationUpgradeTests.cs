using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises the actual audited registration transition and its independent native invalidation boundaries on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRegistrationUpgradeTests(BootstrapFixture fixture)
{
    private const long TargetRevision = 2;
    private const string ProviderTrait = "Provider";
    private const string DifferentActor = "different-deployment-operator";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] States = [true, false];
    private static readonly UpgradeDrift[] BeforeDrifts = [UpgradeDrift.Revision, UpgradeDrift.ApplicationDisabled, UpgradeDrift.ScopeDisabled,
        UpgradeDrift.ExtraProperty, UpgradeDrift.RequestCase, UpgradeDrift.StatusCase];
    private static readonly UpgradeDrift[] AfterDrifts = [UpgradeDrift.ReceiptActor, UpgradeDrift.ReceiptOutcome, UpgradeDrift.DuplicateReceipt];

    /// <summary>Enabled and disabled profiles advance once, preserving native identities and scope; exact replay rolls back all lock-only writes.</summary>
    [Theory, MemberData(nameof(StateCases))]
    public async Task Given_ExactOldRegistration_When_ExplicitUpgradeAndReplayRun_Then_StateIsPreservedAndOnlyOneTransitionCommits(string provider, bool enabled)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var input = BrowserUpgradeFixture.Input(scenario);
        if (!enabled)
        {
            await BrowserUpgradeFixture.ChangeAsync(scenario, UpgradeDrift.DisabledProfile, input);
            input = input with
            {
                ExpectedRevision = TargetRevision
            };
        }
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        Assert.True(before.Legacy && !before.Browser);
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        var after = await BrowserUpgradeFixture.ReadAsync(scenario);
        Assert.True(after.Browser && !after.Legacy);
        Assert.Equal(enabled, after.Enabled);
        Assert.Equal(before.Application, after.Application);
        Assert.Equal(before.Scope, after.Scope);
        Assert.Equal(before.ScopeDescriptor, after.ScopeDescriptor);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.True(after.Observed >= before.Observed);
        await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        Assert.False(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        Assert.True(after == await BrowserUpgradeFixture.ReadAsync(scenario));
        if (!enabled)
        {
            await using var peer = scenario.CreatePeer(provider, policy: input.Policy.ForBrowserSessions());
            NativeAccountDenialTests.RequireJson(await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath,
                NativeAccountFlow.PushFields(scenario), services: peer.Services), OpenIddictConstants.Errors.InvalidClient);
        }
    }

    /// <summary>Native/profile drift is rejected unchanged, including case variants whose bulk SQL behavior would otherwise depend on collation.</summary>
    [Theory, MemberData(nameof(BeforeCases))]
    public async Task Given_DriftedOldRegistration_When_UpgradeRuns_Then_NoMetadataTokenOrAuditChangeCommits(string provider, UpgradeDrift drift)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var input = BrowserUpgradeFixture.Input(scenario);
        _ = await NativeAccountFlow.PushAsync(scenario);
        await BrowserUpgradeFixture.ChangeAsync(scenario, drift, input);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        await BrowserUpgradeFixture.RejectAsync(scenario, input);
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
    }

    /// <summary>An upgraded marker cannot legitimize mismatched or duplicate persisted reconciliation receipts.</summary>
    [Theory, MemberData(nameof(AfterCases))]
    public async Task Given_UpgradedRegistrationWithReceiptDrift_When_Replayed_Then_TheDriftIsRejectedWithoutRepair(string provider, UpgradeDrift drift)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var input = BrowserUpgradeFixture.Input(scenario);
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        await BrowserUpgradeFixture.ChangeAsync(scenario, drift, input);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        await BrowserUpgradeFixture.RejectAsync(scenario, input);
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
    }

    /// <summary>Different operations, actors and original preconditions cannot claim a successful prior command or revoke new requests.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_CommittedUpgrade_When_ReconciliationInputChanges_Then_OnlyTheOriginalCommandCanReturnUnchanged(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var input = BrowserUpgradeFixture.Input(scenario);
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        await using var peer = scenario.CreatePeer(provider, policy: input.Policy.ForBrowserSessions());
        var fresh = await NativeAccountFlow.PushAsync(scenario, peer.Services);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, fresh);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        AdministratorRegistrationInput[] substitutions = [input with { OperationId = Guid.NewGuid() }, input with { OperatorReference = DifferentActor }, input with { ExpectedRevision = TargetRevision }];
        foreach (var changed in substitutions)
        {
            await BrowserUpgradeFixture.RejectAsync(scenario, changed);
            Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
        }
        Assert.False(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, identifier));
    }

    /// <summary>Old codes remain cryptographically valid but fail revision binding; pending PAR is separately revoked and fresh B3 grants succeed.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_GenuineOldCodeAndPendingPar_When_UpgradeCommits_Then_IndependentInvalidationPreservesFreshB3Issuance(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var input = BrowserUpgradeFixture.Input(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, await NativeAccountFlow.PushAsync(scenario)));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var pending = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, pending);
        var unrelated = await BrowserUpgradeEffects.SeedUnrelatedAsync(scenario);
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        await BrowserUpgradeEffects.RequireUnrelatedAsync(scenario, unrelated);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await NativeAccountPersistence.StatusAsync(scenario, identifier));
        var original = await NativeAuthorityCodeOracle.ReadValidArtifactAsync(scenario, code);
        Assert.Equal("1", original.GetClaim(AdministratorPrincipal.ProfileRevision));
        Assert.True(original.GetTokenId() == principal.GetTokenId());
        await using var peer = scenario.CreatePeer(provider, policy: input.Policy.ForBrowserSessions());
        await BrowserUpgradeEffects.RequireFreshGrantAsync(scenario, peer.Services);
        NativeAccountDenialTests.RequireJson(await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), OpenIddictConstants.Errors.InvalidGrant);
        NativeIssuerReplayTests.RequireParReplay(await NativeAccountFlow.AuthorizeAsync(scenario, pending, peer.Services));
        await BrowserUpgradeEffects.RequireFreshGrantAsync(scenario, peer.Services);
    }

    public static IEnumerable<TheoryDataRow<string>> ProviderCases => Providers.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
    public static IEnumerable<TheoryDataRow<string, bool>> StateCases => from provider in Providers
                                                                         from enabled in States
                                                                         select new TheoryDataRow<string, bool>(provider, enabled).WithTrait(ProviderTrait, provider);
    public static IEnumerable<TheoryDataRow<string, UpgradeDrift>> BeforeCases => from provider in Providers
                                                                                  from drift in BeforeDrifts
                                                                                  select new TheoryDataRow<string, UpgradeDrift>(provider, drift).WithTrait(ProviderTrait, provider);
    public static IEnumerable<TheoryDataRow<string, UpgradeDrift>> AfterCases => from provider in Providers
                                                                                 from drift in AfterDrifts
                                                                                 select new TheoryDataRow<string, UpgradeDrift>(provider, drift).WithTrait(ProviderTrait, provider);
}
