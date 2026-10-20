using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves actual authorize/exchange issuance serializes with independent committed source authority changes.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAuthorityRaceTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string AccountLock = "AdministratorAuthentication";
    private const string GovernanceLock = "AdministratorGovernance";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Orders = [false, true];

    /// <summary>Both commit orders retain native consumption and exact row/audit outcomes, with no stale cookie becoming issuance authority.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_IndependentAuthorityChange_When_ActualIssuanceContendsAtCommit_Then_OnlyTheFirstCommittedAuthorityAllowsCredentials(
        string provider, bool authorization, bool issuanceFirst, int changeValue)
    {
        var change = (NativeAuthorityMutation.Change)changeValue;
        var probe = new NativeAuthorityRaceProbe(issuanceFirst, change == NativeAuthorityMutation.Change.Session ? AccountLock : GovernanceLock);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: probe.Configure);
        await using var mutation = await NativeAuthorityMutation.PrepareAsync(scenario, change, probe);
        var attempt = await NativeAccountAttempt.PrepareAsync(scenario, authorization);
        probe.Endpoint = authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token;
        var result = await NativeAuthorityRace.InvokeAsync(provider, scenario, attempt, mutation, probe, issuanceFirst);
        probe.Endpoint = null;
        await mutation.RequirePersistedAsync(scenario, change);
        await NativeAuthorityRaceOutcome.RequireAsync(scenario, attempt, result, authorization, issuanceFirst, change);
    }

    /// <summary>Separately discovers every provider, native endpoint, commit order and source-loss boundary.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, bool, int>> Cases =>
        from provider in Providers
        from authorization in Orders
        from issuanceFirst in Orders
        from change in Enum.GetValues<NativeAuthorityMutation.Change>()
        select new TheoryDataRow<string, bool, bool, int>(provider, authorization, issuanceFirst, (int)change).WithTrait(ProviderTrait, provider);
}
