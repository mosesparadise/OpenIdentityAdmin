using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Joins actual sole-administrator HTTP recovery to independent last-survivor and current-governance authority checks.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryTests(BootstrapFixture fixture)
{
    private const string Sole = "sole-admin-recovery";
    private const string DistinctCodes = "Replacement recovery material must not reuse previously disclosed values.";

    /// <summary>Enumerates the exact F14 sole-administrator recovery row for both native databases.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(Sole);

    /// <summary>Requires single-use restricted recovery before new native factor/session authority while preserving the committed governance revision.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_SoleAdministrator_When_ExistingRecoveryCompletes_Then_RestrictedAuthorityAndGovernanceRemainDistinct(string provider, string condition)
    {
        Assert.Equal(Sole, condition);
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        await GovernanceHttpAuthority.RequireAsync(browser, authorized: true);
        await using var bridge = await GovernanceHttpBridge.CreateAsync(environment, provider, enrolled);
        var revision = await GovernanceRecoveryState.PrepareAsync(bridge);
        var saved = GovernanceHttpAuthority.PreserveCookie(environment, cookies);
        var before = await GovernanceRecoveryState.CodesAsync(bridge);
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Recovery);
        var challenge = await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
        using var secondary = environment.Browser(cookies, secondary: true);
        var replacement = await SecurityHttp.TransitionAsync(secondary, challenge, enrolled.RecoveryCodes[0], recovery: true);
        var destination = await GovernanceRecoveryState.RequireRestrictedAsync(bridge, before, enrolled.RecoveryCodes[0], revision);
        await GovernanceHttpAuthority.RequireAsync(secondary, authorized: false);
        await RequireSingleUseAsync(environment, bridge, enrolled.RecoveryCodes[0], revision);
        var confirmation = await AdministratorJourney.ReadAsync(browser, replacement, enrollment: true);
        await CompleteAsync(secondary, confirmation, enrolled);
        await GovernanceHttpAuthority.RequireAsync(browser, authorized: true);
        using var stale = environment.Browser(saved, secondary: true);
        await GovernanceHttpAuthority.RequireAsync(stale, authorized: false);
        var actor = await GovernanceRecoveryState.RequireCompletedAsync(bridge, confirmation.Seed!, destination, revision);
        await GovernanceRecoveryState.RequireGovernanceAsync(bridge, actor, revision);
        using var replayed = await secondary.GetAsync(replacement, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);
    }

    /// <summary>Uses a fresh admitted password interaction and valid antiforgery so spent-code rejection cannot be attributed to an old form binding.</summary>
    private static async Task RequireSingleUseAsync(AdministratorEnvironment environment, GovernanceHttpBridge bridge, string code, long revision)
    {
        using var browser = environment.Browser(new CookieContainer(), secondary: true);
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Recovery);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
        var before = await GovernanceRecoveryState.CodesAsync(bridge);
        using var repeated = await AdministratorJourney.ConfirmAtAsync(browser, form, code, SecurityHttp.RecoveryVerify);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        await GovernanceRecoveryState.RequireCodesUnchangedAsync(bridge, before);
        var observed = await bridge.ObserveAsync();
        Assert.Equal(revision, observed.AuthorityRevision);
        Assert.Empty(observed.UsableAdministrators);
    }

    /// <summary>Completes actual cross-node native replacement proof and requires fresh recovery material without exposing its values.</summary>
    private static async Task CompleteAsync(HttpClient browser, AdministratorForm confirmation, SecurityEnrollment original)
    {
        var code = FactorOracle.Code(confirmation.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var completed = await AdministratorJourney.ConfirmAtAsync(browser, confirmation, code, SecurityHttp.Confirm);
        FactorOracle.RequireWindow(confirmation.Seed!, code, started, true);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var codes = await SecurityHttp.CodesAsync(completed);
        Assert.False(codes.Intersect(original.RecoveryCodes, StringComparer.Ordinal).Any(), DistinctCodes);
        using var repeated = await AdministratorJourney.ConfirmAtAsync(browser, confirmation, code, SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
    }
}
