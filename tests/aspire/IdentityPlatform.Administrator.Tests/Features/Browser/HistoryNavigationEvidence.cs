using System.Diagnostics;
using System.Globalization;
using System.Text;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains bounded synthetic navigation categories without URLs, document content or request material.</summary>
internal sealed class HistoryNavigationEvidence : IDisposable
{
    private const int MaximumEvents = 64;
    private const int MaximumCount = 1000;
    private const string RootPath = "/";
    private const string PostPath = "/post";
    private const string PhaseRecord = "phase";
    private static readonly CompositeFormat CatchTemplate = CompositeFormat.Parse("catch responses={0} commits={1}");
    private static readonly CompositeFormat ResponseTemplate = CompositeFormat.Parse("response main={0} navigation={1} route={2} method={3} status={4}");
    private static readonly CompositeFormat CommitTemplate = CompositeFormat.Parse("commit main={0} route={1}");
    private static readonly CompositeFormat EventTemplate = CompositeFormat.Parse("history-navigation ordinal={0} elapsed-ms={1} phase={2} {3}");
    private static readonly CompositeFormat FinalTemplate = CompositeFormat.Parse("history-navigation-final overflow={0} posts={1}");
    private readonly IPage page;
    private readonly HistoryControlServer server;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<string> events = [];
    private readonly object gate = new();
    private HistoryNavigationPhase phase;
    private bool overflow;

    /// <summary>Starts passive observation before setup, so earlier navigation events retain their phase.</summary>
    internal HistoryNavigationEvidence(IPage page, HistoryControlServer server)
    {
        this.page = page;
        this.server = server;
        page.Response += Response;
        page.FrameNavigated += Committed;
    }

    /// <summary>Records a code-owned phase under the same ordering lock used by navigation events.</summary>
    internal void MoveTo(HistoryNavigationPhase next)
    {
        lock (gate)
        {
            phase = next;
            Record(PhaseRecord);
        }
    }

    /// <summary>Records the exact counter snapshot used by the unchanged native-refusal decision.</summary>
    internal void Catch(int responses, int commits) => Record(string.Format(CultureInfo.InvariantCulture,
        CatchTemplate, Math.Clamp(responses, 0, MaximumCount), Math.Clamp(commits, 0, MaximumCount)));

    /// <summary>Projects only finite response categories; no actual URL, method, headers or body is retained.</summary>
    private void Response(object? sender, IResponse response)
    {
        var method = HttpMethods.IsGet(response.Request.Method) ? HistoryMethod.Get :
            HttpMethods.IsPost(response.Request.Method) ? HistoryMethod.Post : HistoryMethod.Other;
        var status = response.Status switch
        {
            StatusCodes.Status200OK => HistoryStatus.Ok,
            StatusCodes.Status204NoContent => HistoryStatus.NoContent,
            _ => HistoryStatus.Other
        };
        Record(string.Format(CultureInfo.InvariantCulture, ResponseTemplate, response.Frame == page.MainFrame,
            response.Request.IsNavigationRequest, Route(response.Url), method, status));
    }

    /// <summary>Records successful frame notifications independently of the acceptance observer.</summary>
    private void Committed(object? sender, IFrame frame) =>
        Record(string.Format(CultureInfo.InvariantCulture, CommitTemplate, frame == page.MainFrame, Route(frame.Url)));

    /// <summary>Matches only exact synthetic addresses; unrecognized or query-bearing addresses become other.</summary>
    private HistoryRoute Route(string address)
    {
        if (address == server.Origin || address == server.Origin + RootPath)
        {
            return HistoryRoute.Form;
        }

        if (address == server.Origin + PostPath)
        {
            return HistoryRoute.Post;
        }

        if (address == server.Origin + HistoryControlServer.SafePath)
        {
            return HistoryRoute.Safe;
        }

        if (address == server.Origin + HistoryControlServer.NoContentPath)
        {
            return HistoryRoute.NoContent;
        }

        return HistoryRoute.Other;
    }

    /// <summary>Caps retained events without changing the acceptance observer or suppressing its events.</summary>
    private void Record(string category)
    {
        lock (gate)
        {
            if (events.Count == MaximumEvents)
            {
                overflow = true;
                return;
            }
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            events.Add(string.Format(CultureInfo.InvariantCulture, EventTemplate, events.Count + 1, elapsed, phase, category));
        }
    }

    /// <summary>Detaches before browser/server cleanup and retains the POST count even when the original oracle throws.</summary>
    public void Dispose()
    {
        page.Response -= Response;
        page.FrameNavigated -= Committed;
        lock (gate)
        {
            foreach (var item in events)
            {
                Write(item);
            }

            Write(string.Format(CultureInfo.InvariantCulture,
                FinalTemplate, overflow, Math.Clamp(server.Posts, 0, MaximumCount)));
        }
    }

    /// <summary>Missing output makes the diagnostic incomplete; it must never replace the original browser failure.</summary>
    private static void Write(string line)
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
        }
        catch (Exception) { /* Preserve the original browser result. */ }
    }
}

internal enum HistoryNavigationPhase
{
    Setup, Submit, Safe, Back, Controls, Complete
}

internal enum HistoryRoute
{
    Form, Post, Safe, NoContent, Other
}

internal enum HistoryMethod
{
    Get, Post, Other
}

internal enum HistoryStatus
{
    Ok, NoContent, Other
}
