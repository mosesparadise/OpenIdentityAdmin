using System.Net;
using IdentityPlatform.Sample.Client.Features.Sessions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Forces failures in actual cookie publication after native callback validation and proves server authority is removed.</summary>
public sealed class SampleCallbackFailureTests
{
    private const string CookieFailureMessage = "Synthetic cookie publication failure.";
    private const string ResponseFailureMessage = "Synthetic response publication failure.";
    private const string SetCookieHeader = "Set-Cookie";
    private const int WitnessSeconds = 5;

    /// <summary>A native OnStarting callback that ignores cancellation cannot retain session admission or browser gate ownership.</summary>
    [Fact]
    public async Task Given_PausedNativeHeaders_When_RequestCancels_Then_AdmissionEndsBeforeCallbackRelease()
    {
        using var issuer = new SampleNativeIssuer();
        var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var browser = new SampleNativeClient(issuer, services => services.PostConfigure<CookieAuthenticationOptions>(SampleTickets.Scheme, options =>
            options.Events.OnSignedIn = context =>
            {
                context.Response.OnStarting(async () =>
                {
                    entered.TrySetResult(context.Principal!.FindFirst(SampleTickets.BrowserClaim)!.Value);
                    await release.Task;
                    resumed.TrySetResult();
                });
                return Task.CompletedTask;
            }));
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await SampleNativeSignInTests.ChallengeAsync(browser, issuer);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = browser.SendAsync(HttpMethod.Get, callback, cancellationToken: cancellation.Token);
        string? browserId = null;
        try
        {
            browserId = await entered.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            using var ended = await browser.App.Services.GetRequiredService<BrowserAttempts>().EnterAsync(browserId, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            Assert.Null(ended);
            Assert.Null(browser.App.Services.GetRequiredService<SampleTickets>().FindBrowser(browserId));
        }
        finally
        {
            release.TrySetResult();
            if (entered.Task.IsCompletedSuccessfully)
            {
                await resumed.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>Failure after the native cookie handler stores a ticket must remove that ticket and all success output.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeCookiePublication_When_SignedInOrOnStartingThrows_Then_NoSessionSurvives(bool onStarting)
    {
        using var issuer = new SampleNativeIssuer();
        string? browserId = null;
        await using var browser = new SampleNativeClient(issuer, services => services.PostConfigure<CookieAuthenticationOptions>(SampleTickets.Scheme, options =>
            options.Events.OnSignedIn = context =>
            {
                browserId = context.Principal!.FindFirst(SampleTickets.BrowserClaim)!.Value;
                Assert.NotNull(context.HttpContext.RequestServices.GetRequiredService<SampleTickets>().FindBrowser(browserId));
                if (!onStarting)
                {
                    throw new InvalidOperationException(CookieFailureMessage);
                }
                context.Response.OnStarting(() => throw new InvalidOperationException(ResponseFailureMessage));
                return Task.CompletedTask;
            }));
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await SampleNativeSignInTests.ChallengeAsync(browser, issuer);
        try
        {
            using var response = await browser.SendAsync(HttpMethod.Get, callback);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(response.Headers.TryGetValues(SetCookieHeader, out _));
            Assert.Null(response.Headers.Location);
        }
        catch (Exception exception) when (onStarting && (exception is HttpRequestException or OperationCanceledException || exception is InvalidOperationException { Message: ResponseFailureMessage })) { /* A failed started response is aborted instead of completed. */ }
        Assert.NotNull(browserId);
        Assert.Null(browser.App.Services.GetRequiredService<SampleTickets>().FindBrowser(browserId));
        Assert.Null(await browser.App.Services.GetRequiredService<BrowserAttempts>().EnterAsync(browserId, TestContext.Current.CancellationToken));
    }

    /// <summary>A real native signing event resuming after cancellation cannot create authority after the callback cleanup has ended admission.</summary>
    [Fact]
    public async Task Given_DelayedNativeSigning_When_RequestCancels_Then_LateStoreCannotReviveSession()
    {
        using var issuer = new SampleNativeIssuer();
        var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var browser = new SampleNativeClient(issuer, services => services.PostConfigure<CookieAuthenticationOptions>(SampleTickets.Scheme, options =>
            options.Events.OnSigningIn = async context =>
            {
                entered.TrySetResult(context.Principal!.FindFirst(SampleTickets.BrowserClaim)!.Value);
                await release.Task;
                resumed.TrySetResult();
            }));
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await SampleNativeSignInTests.ChallengeAsync(browser, issuer);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = browser.SendAsync(HttpMethod.Get, callback, cancellationToken: cancellation.Token);
        string? browserId = null;
        try
        {
            browserId = await entered.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            using var ended = await browser.App.Services.GetRequiredService<BrowserAttempts>().EnterAsync(browserId, TestContext.Current.CancellationToken);
            Assert.Null(ended);
        }
        finally
        {
            release.TrySetResult();
            if (entered.Task.IsCompletedSuccessfully)
            {
                await resumed.Task.WaitAsync(TimeSpan.FromSeconds(WitnessSeconds), TestContext.Current.CancellationToken);
            }
        }
        Assert.Null(browser.App.Services.GetRequiredService<SampleTickets>().FindBrowser(browserId!));
        Assert.NotNull(browserId);
        var ticket = SampleSessionTests.Ticket(browserId, DateTimeOffset.UtcNow.AddMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => browser.App.Services.GetRequiredService<SampleTickets>().StoreAsync(ticket));
    }
}
