using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Orders native authenticated HTTP publication against original expiry and terminal logout.</summary>
public sealed class SamplePublicationTests
{
    private const string Profile = "/profile";
    private const string Logout = "/logout";
    private const string Home = "/";
    private const string IdentityText = "synthetic@example.invalid";
    private const int WitnessSeconds = 5;
    private const int ExpiredMinutes = 6;

    /// <summary>An admitted response holds ownership through OnStarting; logout cannot finish until publication releases it.</summary>
    [Fact]
    [Trait("CriticalDecision", "sample-publication-gate")]
    public async Task Given_ProfilePublicationOwnsGate_When_LogoutArrives_Then_ResultFinishesBeforeTerminalLogout()
    {
        using var issuer = new SampleNativeIssuer();
        await using var browser = new SampleNativeClient(issuer);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logoutEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationReleased = false;
        var logoutBeforePublication = false;
        browser.App.Use(async (context, next) =>
        {
            if (context.Request.Path == Profile)
            {
                context.Response.OnStarting(async () => { entered.TrySetResult(); await release.Task; publicationReleased = true; });
            }
            if (context.Request.Path == Logout)
            {
                context.Response.OnStarting(() => { logoutBeforePublication = !publicationReleased; return Task.CompletedTask; });
            }
            var operation = next(context);
            if (context.Request.Path == Logout)
            {
                logoutEntered.TrySetResult();
            }
            await operation;
        });
        await SignInAsync(browser, issuer);
        using var form = await browser.FormAsync();
        var profile = browser.PostAsync(Profile);
        Task<HttpResponseMessage>? logout = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            logout = browser.SendAsync(HttpMethod.Post, Logout, form);
            await logoutEntered.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);

        }
        finally { release.TrySetResult(); }
        using var published = await profile;
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Contains(IdentityText, await published.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        using var ended = await logout!;
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        const string bypass = "sample-publication-gate-bypass";
        if (logoutBeforePublication)
        {
            Assert.Fail(bypass);
        }
        using var home = await browser.SendAsync(HttpMethod.Get, Home);
        Assert.DoesNotContain("Signed in as", await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Advancing the original clock after resource success but before headers aborts actual HTTP publication.</summary>
    [Fact]
    public async Task Given_ProfileReady_When_OriginalExpiryPassesBeforeHeaders_Then_NoIdentityResponseIsPublished()
    {
        using var issuer = new SampleNativeIssuer();
        var clock = new SampleClock { Now = DateTimeOffset.UtcNow };
        await using var browser = new SampleNativeClient(issuer, services => services.AddSingleton<TimeProvider>(clock));
        var reached = false;
        browser.App.Use(async (context, next) =>
        {
            if (context.Request.Path == Profile)
            {
                context.Response.OnStarting(() =>
                {
                    reached = true;
                    clock.Now += TimeSpan.FromMinutes(ExpiredMinutes);
                    return Task.CompletedTask;
                });
            }
            await next(context);
        });
        await SignInAsync(browser, issuer);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => browser.PostAsync(Profile));
        Assert.True(reached);
        Assert.Equal(1, issuer.UserInfoCalls);
        using var home = await browser.SendAsync(HttpMethod.Get, Home);
        Assert.DoesNotContain("Signed in as", await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Obtains genuine native state, cryptographically validated identity and a server-held cookie ticket.</summary>
    private static async Task SignInAsync(SampleNativeClient browser, SampleNativeIssuer issuer)
    {
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await SampleNativeSignInTests.ChallengeAsync(browser, issuer);
        using var response = await browser.SendAsync(HttpMethod.Get, callback);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
