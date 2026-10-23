using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual registration-read or profile-lock contention between the audited upgrade and native PAR admission/issuance in both commit orders.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserUpgradeIssuanceRaceTests(BootstrapFixture fixture)
{
    private const string SqlServer = "SqlServer";
    private const string ProfileLock = "AdministratorProtocolProfile";
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Orders = [true, false];

    /// <summary>Upgrade-first denies old-profile admission; issuance-first leaves its returned old PAR durably revoked, while new B3 issuance remains usable.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_NativeParAndExplicitUpgrade_When_ActualRegistrationOwnershipContends_Then_CommitOrderCannotLeaveAnOldPendingRequestUsable(string provider, bool upgradeFirst)
    {
        var issuance = new BrowserUpgradeRaceProbe(!upgradeFirst, true, ProfileLock);
        var upgrade = new BrowserUpgradeRaceProbe(upgradeFirst, false, ProfileLock);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: issuance.Configure);
        await using var operatorRoot = scenario.CreatePeer(provider, upgrade.Configure);
        _ = await NativeAccountFlow.PushAsync(scenario, operatorRoot.Services);
        var input = BrowserUpgradeFixture.Input(scenario);
        var observer = new NativeLockObservation(provider, scenario.Account.Journey.Lease.AdministratorConnection);
        Func<Task<object>> issue = async () => await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, NativeAccountFlow.PushFields(scenario));
        Func<Task<object>> change = async () => await BrowserUpgradeFixture.UpgradeAsync(scenario, input, operatorRoot.Services);
        issuance.Armed = true;
        upgrade.Armed = true;
        object[] outcomes;
        try
        {
            var winner = upgradeFirst ? upgrade : issuance;
            var peer = upgradeFirst ? issuance : upgrade;
            outcomes = await NativeBlockedRace.InvokeAsync(upgradeFirst ? change : issue, winner.Reached.Task,
                upgradeFirst ? issue : change, peer.Reached.Task, winner.Release, observer.WaitAsync);
            Assert.Equal(upgradeFirst && provider == SqlServer, issuance.AdmissionReadObserved);
            Assert.Equal(1, upgrade.Commits);
            Assert.Equal(upgradeFirst ? 0 : 1, issuance.Commits);
        }
        finally { issuance.Armed = false; upgrade.Armed = false; }
        Assert.True(Assert.IsType<bool>(outcomes[upgradeFirst ? 0 : 1]));
        var response = Assert.IsType<NativeAccountResult>(outcomes[upgradeFirst ? 1 : 0]);
        await RequireOutcomeAsync(scenario, response, upgradeFirst);
        await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        await using var browser = scenario.CreatePeer(provider, policy: input.Policy.ForBrowserSessions());
        await BrowserUpgradeEffects.RequireFreshGrantAsync(scenario, browser.Services);
    }

    /// <summary>Separates definite denied admission from an earlier committed reference invalidated by the subsequent upgrade.</summary>
    private static async Task RequireOutcomeAsync(AdministratorProtocolScenario scenario, NativeAccountResult response, bool upgradeFirst)
    {
        if (upgradeFirst)
        {
            NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        var reference = NativeAccountFlow.ReadReference(response);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await NativeAccountPersistence.StatusAsync(scenario, identifier));
    }

    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in Providers
                                                                    from upgradeFirst in Orders
                                                                    select new TheoryDataRow<string, bool>(provider, upgradeFirst).WithTrait(ProviderTrait, provider);
}
