using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Distinguishes definite rollback from committed-but-unpublished issuer logout on both actual providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutFailureTests(BootstrapFixture fixture)
{
    private const int ConfirmationSeconds = 300;
    private const int ExpireAfterCleanup = 2;
    private const string ProviderTrait = "Provider";
    private static readonly bool[] SaveVariants = [false, true];
    private static readonly int[] PublicationVariants = [0, 1, ExpireAfterCleanup];
    private const string CleanupFailure = "Synthetic issuer logout cleanup failure.";

    /// <summary>Enumerates confirmation expiry and clock rollback at the genuine final persistence seam.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> SaveCases => from provider in IssuerContinuationProviderFixture.Providers
                                                                        from variant in SaveVariants
                                                                        select new TheoryDataRow<string, bool>(provider, variant).WithTrait(ProviderTrait, provider);

    /// <summary>Neither expiry nor rollback after a real save can commit revocation; removing the fault preserves the same-form positive control.</summary>
    [Theory, MemberData(nameof(SaveCases))]
    public async Task Given_ValidConfirmation_When_ClockLeavesOriginalWindowDuringSave_Then_RevocationAndAuditRollBack(string provider, bool rollback)
    {
        var fault = new IssuerLogoutFault();
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider, fault.Configure);
        var form = await scenario.FormAsync();
        var clock = scenario.Login.Issuer.Account.Journey.Clock;
        var original = clock.GetUtcNow();
        fault.AfterSave = () => clock.Now = rollback ? original.AddSeconds(-1) : DateTimeOffset.FromUnixTimeSeconds(original.ToUnixTimeSeconds() + ConfirmationSeconds);
        using var denied = await scenario.PostAsync(form);
        IssuerLogoutTests.RequireDenied(denied);
        Assert.Equal(1, fault.Saves);
        Assert.Equal(0, fault.Commits);
        var effects = await scenario.EffectsAsync();
        Assert.True(effects.Sessions.Values.All(revoked => !revoked));
        Assert.Equal(0, effects.Audits);
        fault.AfterSave = null;
        clock.Now = original;
        using var control = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(control);
        await scenario.RequireRevokedAsync();
        Assert.Equal(1, fault.Commits);
    }

    /// <summary>Enumerates lost native acknowledgement, failed actual cleanup and confirmation expiry during actual cleanup.</summary>
    public static IEnumerable<TheoryDataRow<string, int>> PublicationCases => from provider in IssuerContinuationProviderFixture.Providers
                                                                              from variant in PublicationVariants
                                                                              select new TheoryDataRow<string, int>(provider, variant).WithTrait(ProviderTrait, provider);

    /// <summary>A real committed issuer effect never becomes a false rollback claim or a published success after uncertain cleanup.</summary>
    [Theory, MemberData(nameof(PublicationCases))]
    public async Task Given_ConfirmedLogout_When_AcknowledgementOrCleanupFails_Then_OneCommitSurvivesWithoutSuccessPublication(string provider, int mode)
    {
        var fault = new IssuerLogoutFault();
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider, fault.Configure);
        var form = await scenario.FormAsync();
        var clock = scenario.Login.Issuer.Account.Journey.Clock;
        var expires = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds() + ConfirmationSeconds);
        fault.LoseAcknowledgement = mode == 0;
        if (mode == 1)
        {
            fault.AfterCleanup = () => throw new InvalidOperationException(CleanupFailure);
        }
        if (mode == ExpireAfterCleanup)
        {
            fault.AfterCleanup = () => clock.Now = expires;
        }
        using var response = await scenario.PostAsync(form);
        await IssuerLogoutTests.RequireUnavailableAsync(response);
        Assert.Equal(1, fault.Saves);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Cleanups);
        await scenario.RequireRevokedAsync();
        using var replay = await scenario.PostAsync(form);
        IssuerLogoutTests.RequireDenied(replay);
        Assert.Equal(1, fault.Commits);
        await scenario.RequireRevokedAsync();
    }

    /// <summary>Actual database audit denial rolls back revocation; restoring privilege allows the original explicit confirmation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AuditInsertDenied_When_NativeLogoutIsConfirmed_Then_NoRevocationSurvivesAndRestoredControlCommits(string provider)
    {
        var observation = new BrowserIssuerPermissionFailure();
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider, null, observation);
        var form = await scenario.FormAsync();
        await IssuerAuditPermission.RunAsync(scenario.Login.Issuer.Account.Journey.Lease, async () =>
        {
            using var response = await scenario.PostAsync(form);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            IssuerLogoutTests.RequireDenied(response);
            Assert.Equal(1, observation.Failures);
            var effects = await scenario.EffectsAsync();
            Assert.True(effects.Sessions.Values.All(revoked => !revoked));
            Assert.Equal(0, effects.Audits);
        });
        using var control = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(control);
        await scenario.RequireRevokedAsync();
    }
}
