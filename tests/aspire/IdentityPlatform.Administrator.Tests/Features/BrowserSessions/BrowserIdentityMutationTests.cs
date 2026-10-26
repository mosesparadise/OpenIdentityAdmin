using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves specific B3 identity-boundary bypasses after genuine native signature, nonce, state and exchange validation, without a masking B1 issuer.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIdentityMutationTests(BootstrapFixture fixture)
{
    private const string IntentMarker = "browser-intent-binding-bypass";
    private const string ExpiryMarker = "browser-source-deadline-bypass";
    private const string IntentClaim = "admin_login_intent";
    private const string ExpiryClaim = "admin_session_expires_at";

    /// <summary>A valid independently re-signed wrong intent cannot authenticate, despite successful native consumption and otherwise genuine identity.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-intent-binding")]
    public async Task Given_RealNativeCallback_When_OnlySignedIntentDiffers_Then_AuthenticationRejectsThatExactBinding(string provider)
    {
        Assert.True(await RunAsync(provider, null, false));
        var bypass = await RunAsync(provider, null, true);
        Assert.False(bypass, IntentMarker);
    }

    /// <summary>Native B3 authentication rejects original source expiry equality even while ordinary ID-token expiry remains valid; this makes no B1 resurrection claim.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-source-deadline")]
    public async Task Given_RealNativeCallback_When_OnlySignedSourceDeadlineEqualsNow_Then_AuthenticationRejectsExpiredSource(string provider)
    {
        Assert.True(await RunAsync(provider, null, false));
        var bypass = await RunAsync(provider, NativeIdentityResponse.Fault.BrowserExpiryEquality, false);
        Assert.False(bypass, ExpiryMarker);
    }

    /// <summary>Runs a fresh real ceremony and independent callback root, verifying exact native effects before returning the targeted authentication outcome.</summary>
    private async Task<bool> RunAsync(string provider, NativeIdentityResponse.Fault? fault, bool mismatch)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var ordinary = NativeClientInteroperabilityTests.Profile(keys);
        var profile = ordinary.ForBrowserSessions();
        await using var browser = await NativeBrowserParFixture.CreateAsync(fixture, provider, fullProtocol: true, policy: ordinary.Registration);
        var row = await browser.RowAsync();
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var first = new NativeClientHttp(database, browser.Scenario, profile, keys,
            services => BrowserProtocolClientTests.Capture(services, profile, browser, row.ExpiresAt), protocolServices: browser.Services);
        await using var second = new NativeClientHttp(database, browser.Scenario, profile, keys, protocolServices: browser.Services);
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(first);
        var callback = await BrowserProtocolClientTests.AuthorizeAsync(browser, challenge.Location);
        second.Bridge.RewriteBrowserIntent = mismatch;
        second.Bridge.IdentityFault = fault;
        var response = await second.InvokeAsync(callback, challenge.Cookie);
        NativeClientInteroperabilityTests.RequireClean(response);
        var authenticated = response.Authentication?.Succeeded == true;
        if (authenticated)
        {
            NativeClientInteroperabilityTests.RequireAccepted(response);
            if (mismatch)
            {
                Assert.True(response.Authentication!.Principal!.GetClaim(IntentClaim) != browser.Binding.IntentDigest);
            }
            else if (fault is not null)
            {
                Assert.True(response.Authentication!.Principal!.GetClaim(ExpiryClaim) == browser.Scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                BrowserProtocolFlow.RequireIdentity(response.Authentication!.Principal!, row);
            }
        }
        else
        {
            Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        }
        await NativeClientInteroperabilityTests.RequireStatesAsync(first, total: 1, redeemed: 1);
        NativeClientDenial.RequireReplay(await first.InvokeAsync(callback, challenge.Cookie));
        Assert.Equal(mismatch || fault is not null ? 1 : 0, second.Bridge.IdentityRewrites);
        Assert.Equal(1, first.Bridge.ParSends + second.Bridge.ParSends);
        Assert.Equal(1, first.Bridge.TokenSends + second.Bridge.TokenSends);
        return authenticated;
    }
}
