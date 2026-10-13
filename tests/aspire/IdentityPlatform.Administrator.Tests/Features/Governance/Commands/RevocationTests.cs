using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Joins native governed account transitions to real HTTP session denial and fresh login on the same issuer and database.</summary>
[Collection("administrator-bootstrap")]
public sealed class RevocationTests(BootstrapFixture fixture)
{
    private const string Disable = "disable-then-enable";
    private const string Suspend = "suspend-then-resume";

    /// <summary>Enumerates the exact C10 disable/enable and suspend/resume rows on each real database.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(Disable, Suspend);

    /// <summary>Requires atomic prior-authority revocation, preserved target security material through reactivation, and genuine new password/TOTP login.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_DisabledOrSuspendedAccount_When_ReenabledOrResumed_Then_OldAuthorityCannotResurrect(string provider, string condition)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        await GovernanceHttpAuthority.RequireAsync(browser, authorized: true);
        await using var bridge = await GovernanceHttpBridge.CreateAsync(environment, provider, enrolled);
        var witness = await bridge.WitnessAsync();
        using var pendingBrowser = environment.Browser(new CookieContainer());
        var pending = await AdministratorJourney.BeginAsync(environment, pendingBrowser, enrollment: false);
        await GovernanceHttpAuthority.SeedLocalAsync(bridge);
        var before = await ReactivationState.ReadAsync(bridge);
        Assert.Equal(1, before.LiveSessions);
        Assert.Equal(1, before.LiveLocalSessions);
        Assert.Equal(1, before.PendingInteractions);
        var saved = GovernanceHttpAuthority.PreserveCookie(environment, cookies);
        var disabled = await TransitionAsync(bridge, witness, condition, restoring: false);
        ReactivationState.Require(before, disabled, condition == Disable, restoring: false);
        using (var stale = environment.Browser(GovernanceHttpAuthority.PreserveCookie(environment, saved), secondary: true))
        {
            await GovernanceHttpAuthority.RequireAsync(stale, authorized: false);
        }
        var restored = await TransitionAsync(bridge, witness, condition, restoring: true);
        ReactivationState.Require(disabled, restored, condition == Disable, restoring: true);
        using var old = environment.Browser(GovernanceHttpAuthority.PreserveCookie(environment, saved), secondary: true);
        await GovernanceHttpAuthority.RequireAsync(old, authorized: false);
        await GovernanceHttpAuthority.RequireConsumedContinuationAsync(pendingBrowser, pending, bridge);
        await GovernanceHttpAuthority.LoginAsync(environment, bridge);
    }

    /// <summary>Runs the real governed command with a separate current administrator and rereads target state after its commit.</summary>
    private static async Task<ReactivationState> TransitionAsync(GovernanceHttpBridge bridge, GovernanceHttpActor witness,
        string condition, bool restoring)
    {
        Assert.True(condition is Disable or Suspend);
        var action = condition == Disable
            ? restoring ? GovernanceAction.EnableAccount : GovernanceAction.DisableAccount
            : restoring ? GovernanceAction.ResumeAccount : GovernanceAction.SuspendAccount;
        var request = await bridge.RequestAsync(action, user: bridge.Original.UserId);
        Assert.Equal(GovernanceOutcome.Accepted, await bridge.ExecuteAsync(witness, request));
        return await ReactivationState.ReadAsync(bridge);
    }
}
