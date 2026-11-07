using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Temporary finite observations distinguish native request completion from error-document commitment without retaining request values.</summary>
internal sealed class CrossSiteNavigationTrace : IDisposable
{
    private const string ReportFormat = "cross-site-navigation status={0} exact-request={1} navigation={2} main-frame={3} redirected-from={4} redirected-to={5} finished={6} failed={7} target-commits={8} other-commits={9} current-target={10} current-staged={11}";
    private readonly IPage page;
    private readonly Uri destination;
    private readonly Uri staged;
    private int finished;
    private int failed;
    private int targetCommits;
    private int otherCommits;

    /// <summary>Attaches passive observers before the real native form submission.</summary>
    internal CrossSiteNavigationTrace(IPage page, Uri destination, Uri staged)
    {
        this.page = page;
        this.destination = destination;
        this.staged = staged;
        page.RequestFinished += Finished;
        page.RequestFailed += Failed;
        page.FrameNavigated += Navigated;
    }

    /// <summary>Records only a completed exact native main-frame POST.</summary>
    private void Finished(object? sender, IRequest request)
    {
        if (Matches(request))
        {
            Interlocked.Increment(ref finished);
        }
    }

    /// <summary>Records only failure of the exact native main-frame POST, never its error string.</summary>
    private void Failed(object? sender, IRequest request)
    {
        if (Matches(request))
        {
            Interlocked.Increment(ref failed);
        }
    }

    /// <summary>Counts committed main-frame destinations as target or other without printing their values.</summary>
    private void Navigated(object? sender, IFrame frame)
    {
        if (frame != page.MainFrame)
        {
            return;
        }
        if (frame.Url == destination.AbsoluteUri)
        {
            Interlocked.Increment(ref targetCommits);
        }
        else
        {
            Interlocked.Increment(ref otherCommits);
        }
    }

    /// <summary>Limits terminal events to the intended real navigation request.</summary>
    private bool Matches(IRequest request) => request.Method == HttpMethods.Post &&
        request.Url == destination.AbsoluteUri && request.IsNavigationRequest && request.Frame == page.MainFrame;

    /// <summary>Reports finite flags and counts after the original wait, whether it passes or fails.</summary>
    internal void Report(IResponse response) => TestContext.Current.TestOutputHelper?.WriteLine(ReportFormat,
        response.Status, response.Url == destination.AbsoluteUri && Matches(response.Request),
        response.Request.IsNavigationRequest, response.Request.Frame == page.MainFrame,
        response.Request.RedirectedFrom is not null, response.Request.RedirectedTo is not null,
        Volatile.Read(ref finished), Volatile.Read(ref failed), Volatile.Read(ref targetCommits),
        Volatile.Read(ref otherCommits), page.Url == destination.AbsoluteUri, page.Url == staged.AbsoluteUri);

    /// <summary>Removes every passive observer without altering the browser or server.</summary>
    public void Dispose()
    {
        page.RequestFinished -= Finished;
        page.RequestFailed -= Failed;
        page.FrameNavigated -= Navigated;
    }
}
