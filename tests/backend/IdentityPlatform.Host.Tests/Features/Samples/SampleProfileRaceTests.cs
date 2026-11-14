using System.Net;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Orders real explicit profile HTTP completion against browser logout without holding a gate over remote work.</summary>
public sealed class SampleProfileRaceTests
{
    private const string ProfilePath = "/profile";
    private const string LogoutPath = "/logout";
    private const int WitnessSeconds = 5;

    /// <summary>Explicit UserInfo works after native sign-in, while a held second response cannot disclose profile after logout wins.</summary>
    [Fact]
    public async Task Given_InFlightCurrentProfile_When_LogoutWins_Then_LateProfileIsNotPublished()
    {
        using var issuer = new SampleNativeIssuer();
        await using var browser = new SampleNativeClient(issuer);
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await SampleNativeSignInTests.ChallengeAsync(browser, issuer);
        using (var signedIn = await browser.SendAsync(HttpMethod.Get, callback))
        {
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        }
        using (var profile = await browser.PostAsync(ProfilePath))
        {
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Contains("synthetic@example.invalid", await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        issuer.BeforeProfile = () => { entered.TrySetResult(); return release.Task; };
        var pending = browser.PostAsync(ProfilePath);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            using var logout = await browser.PostAsync(LogoutPath);
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        }
        finally { release.TrySetResult(); }
        using var result = await pending;
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("synthetic@example.invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Current profile received", body, StringComparison.Ordinal);
        Assert.Equal(2, issuer.UserInfoCalls);
    }
}
