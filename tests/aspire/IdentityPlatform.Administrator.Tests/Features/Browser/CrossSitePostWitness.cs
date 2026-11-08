using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Observes the exact hostile native form POST through clean completion without requiring an error document to render.</summary>
internal sealed class CrossSitePostWitness : IDisposable
{
    private const string Document = "document";
    private const string Location = "location";
    private const string ControlTarget = "https://issuer.example.test/connect/end-session";
    private const string OtherControlTarget = "https://other.example.test/connect/end-session";
    private const string UnexpectedControlQuery = "?unexpected=1";
    private const int TerminalSeconds = 20;
    private readonly IPage page;
    private readonly string target;
    private readonly CrossSitePostReceipt receipt = new();

    /// <summary>Arms request and terminal observers before dispatch, retaining the original twenty-second navigation budget for completion.</summary>
    internal CrossSitePostWitness(IPage page, Uri target)
    {
        this.page = page;
        this.target = target.AbsoluteUri;
        page.Request += Submitted;
        page.RequestFinished += Finished;
        page.RequestFailed += Failed;
    }

    /// <summary>Requires exact response identity, no redirect and one clean terminal event before callers inspect denial and durable state.</summary>
    internal async Task<IResponse> SubmitAsync(Func<Task> click)
    {
        var response = await page.RunAndWaitForResponseAsync(click,
            response => response.Url == target && Capture(response.Request).Matches(target));
        Assert.False((await response.AllHeadersAsync()).ContainsKey(Location));
        await receipt.Terminal.WaitAsync(TimeSpan.FromSeconds(TerminalSeconds), TestContext.Current.CancellationToken);
        Assert.True(Capture(response.Request).Matches(target));
        Assert.True(receipt.Accepts(response.Request));
        Assert.False(page.IsClosed);
        return response;
    }

    /// <summary>Records the exact native form request before any response or terminal event can qualify.</summary>
    private void Submitted(object? sender, IRequest request)
    {
        if (Capture(request).Matches(target))
        {
            receipt.Submit(request);
        }
    }

    /// <summary>Records a completed response body for the matching actual request.</summary>
    private void Finished(object? sender, IRequest request)
    {
        if (Capture(request).Matches(target))
        {
            receipt.End(request, finished: true);
        }
    }

    /// <summary>Wakes the waiter on a failed request but never turns that failure into successful denial evidence.</summary>
    private void Failed(object? sender, IRequest request)
    {
        if (Capture(request).Matches(target))
        {
            receipt.End(request, finished: false);
        }
    }

    /// <summary>Reads only native request identity; sensitive values never become assertion messages or diagnostics.</summary>
    private Identity Capture(IRequest request) => new(request.Url, request.Method, request.ResourceType == Document,
        request.IsNavigationRequest, request.IsNavigationRequest && request.Frame == page.MainFrame,
        request.RedirectedFrom is not null || request.RedirectedTo is not null);

    /// <summary>Rejects unrelated responses, altered destinations, subframes and redirects using the exact real-witness identity predicate.</summary>
    internal static void RequireControls()
    {
        CrossSitePostReceipt.RequireControls();
        var identity = new Identity(ControlTarget, HttpMethods.Post, true, true, true, false);
        Assert.True(identity.Matches(ControlTarget));
        Identity[] rejected = [identity with { Url = ControlTarget + UnexpectedControlQuery },
            identity with { Url = OtherControlTarget },
            identity with { Method = HttpMethods.Get }, identity with { Document = false },
            identity with { Navigation = false }, identity with { MainFrame = false }, identity with { Redirected = true }];
        Assert.All(rejected, candidate => Assert.False(candidate.Matches(ControlTarget)));
    }

    /// <summary>Detaches every observer on successful, failed or cancelled submission without altering the page.</summary>
    public void Dispose()
    {
        page.Request -= Submitted;
        page.RequestFinished -= Finished;
        page.RequestFailed -= Failed;
    }

    /// <summary>Separates a native form request's exact identity from the page's eventual document URL.</summary>
    private readonly record struct Identity(string Url, string Method, bool Document, bool Navigation, bool MainFrame, bool Redirected)
    {
        /// <summary>Admits only one exact document POST from the intended main frame with no redirect chain.</summary>
        internal bool Matches(string target) => Url == target && Method == HttpMethods.Post &&
            Document && Navigation && MainFrame && !Redirected;
    }
}
