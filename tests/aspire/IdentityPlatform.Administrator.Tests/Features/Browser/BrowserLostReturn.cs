using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Interrupts the actual issuer redirect at the BFF transport before product return handling, with a mandatory real browser failure witness.</summary>
internal sealed class BrowserLostReturn(BrowserHttpsFixture fixture)
{
    private const int NavigationSeconds = 20;
    private const string Location = "location";
    private const string Report = "b3-return-loss aborted-gets={0}";
    private readonly TaskCompletionSource<Uri> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int AbortedRequests => fixture.ReturnFailure.Count;
    internal Uri? Destination => fixture.ReturnFailure.Destination;

    /// <summary>Arms the test-only HTTPS transport and observes only a failed GET to the exact product return path.</summary>
    internal Task InstallAsync(IPage page)
    {
        page.RequestFailed += (_, request) =>
        {
            if (request.Method == HttpMethods.Get && Matches(request.Url))
            {
                failed.TrySetResult(new Uri(request.Url));
            }
        };
        fixture.ReturnFailure.Arm();
        return Task.CompletedTask;
    }

    /// <summary>Waits for the actual failed return and handles only the navigation error caused by that witnessed bounded transport failure.</summary>
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
        var redirect = await response;
        Assert.Equal(StatusCodes.Status302Found, redirect.Status);
        var observed = await failed.Task.WaitAsync(TimeSpan.FromSeconds(NavigationSeconds), TestContext.Current.CancellationToken);
        var expected = new Uri(fixture.Profile.Issuer, redirect.Headers[Location]);
        Assert.True(observed == expected && Destination == expected);
        Assert.True(AbortedRequests > 0);
        Assert.NotNull(Destination);
        fixture.ReturnFailure.Disarm();
        TestContext.Current.TestOutputHelper?.WriteLine(Report, AbortedRequests);
    }

    /// <summary>Compares the entire fixed authority/path privately; an arbitrary navigation cannot satisfy the loss witness.</summary>
    private bool Matches(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Return).AbsoluteUri;
}
