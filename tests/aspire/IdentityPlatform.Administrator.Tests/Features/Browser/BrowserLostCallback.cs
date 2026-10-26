using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins a real failed callback navigation before any test submits its private URI from another browser context.</summary>
internal static class BrowserLostCallback
{
    private const string Callback = "/callback";
    private const string Authorize = "/connect/authorize";
    private const string Location = "location";
    private const int ArrivalSeconds = 20;

    /// <summary>Aborts all retries before product dispatch and requires both native issuer redirect and actual browser request-failure witnesses.</summary>
    internal static async Task<Uri> CaptureAsync(BrowserHttpsFixture fixture, IPage page)
    {
        var failed = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new Uri(fixture.Profile.ClientUri, Callback);
        EventHandler<IRequest> observe = (_, request) =>
        {
            if (request.Method == HttpMethods.Get && Matches(request.Url, expected))
            {
                failed.TrySetResult(new Uri(request.Url));
            }
        };
        page.RequestFailed += observe;
        fixture.CallbackFailure.Arm();
        try
        {
            var redirect = page.WaitForResponseAsync(response => response.Request.Method == HttpMethods.Get &&
                Matches(response.Url, new Uri(fixture.Profile.Issuer, Authorize)));
            try
            {
                await BrowserHttpsJourney.FactorAsync(fixture, page);
            }
            catch (PlaywrightException) { /* The exact native redirect and failed request remain mandatory below. */ }
            var response = await redirect;
            Assert.Equal(StatusCodes.Status302Found, response.Status);
            var actual = await failed.Task.WaitAsync(TimeSpan.FromSeconds(ArrivalSeconds), TestContext.Current.CancellationToken);
            Assert.True(actual == new Uri(fixture.Profile.Issuer, response.Headers[Location]) && fixture.CallbackFailure.Destination == actual);
            Assert.True(fixture.CallbackFailure.Count > 0);
            return actual;
        }
        finally
        {
            fixture.CallbackFailure.Disarm();
            page.RequestFailed -= observe;
        }
    }

    /// <summary>Compares only privately retained exact fixed authority/path, never an arbitrary failed request or response.</summary>
    private static bool Matches(string value, Uri expected) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.GetLeftPart(UriPartial.Path) == expected.AbsoluteUri;
}
