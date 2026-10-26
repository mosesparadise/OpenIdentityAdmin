using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Drops only an actual native issuer redirect's ensuing BFF navigation, before the BFF can receive it.</summary>
internal sealed class BrowserLostReturn(BrowserHttpsFixture fixture)
{
    private const int NavigationSeconds = 20;
    private readonly TaskCompletionSource failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int interceptions;
    internal int Interceptions => Volatile.Read(ref interceptions);
    internal Uri? Destination
    {
        get; private set;
    }

    /// <summary>Restricts interception and failure observation to the fixed native return path; it never supplies a synthetic response.</summary>
    internal async Task InstallAsync(IPage page)
    {
        page.RequestFailed += (_, request) =>
        {
            if (Matches(request.Url))
            {
                failed.TrySetResult();
            }
        };
        await page.RouteAsync(Matches, async route =>
        {
            Assert.Equal(HttpMethods.Get, route.Request.Method);
            Assert.Equal(1, Interlocked.Increment(ref interceptions));
            Destination = new Uri(route.Request.Url);
            await route.AbortAsync();
        });
    }

    /// <summary>Waits for the actual failed return and handles only the navigation error caused by that witnessed one-shot interception.</summary>
    internal async Task ConfirmAsync(IPage page)
    {
        var response = page.WaitForResponseAsync(response => response.Request.Method == HttpMethods.Post &&
            Uri.TryCreate(response.Url, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.Issuer, BrowserLogoutJourney.IssuerPath).AbsoluteUri);
        var click = page.GetByRole(AriaRole.Button, new()
        {
            Name = BrowserLogoutJourney.Confirm,
            Exact = true
        }).ClickAsync();
        try
        {
            await click;
        }
        catch (PlaywrightException)
        {
            // Event delivery may follow command rejection. The exact failed return, native redirect and durable effects below remain mandatory.
        }
        Assert.Equal(StatusCodes.Status302Found, (await response).Status);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(NavigationSeconds), TestContext.Current.CancellationToken);
        Assert.Equal(1, Interceptions);
        await page.UnrouteAsync(Matches);
    }

    /// <summary>Compares the entire fixed authority/path privately; an arbitrary navigation cannot satisfy the loss witness.</summary>
    private bool Matches(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Return).AbsoluteUri;
}
