using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Observes one synthetic error document without changing the original history verdict.</summary>
internal sealed class HistoryDocumentEvidence : IDisposable
{
    private const int BudgetMilliseconds = 5000;
    private const int CountLimit = 1000;
    private const string Root = "html";
    private const string OutputPrefix = "history-document ";
    private const string PostPath = "/post";
    private const string Projection = """
        (element, input) => {
            const uri = new URL(document.documentURI);
            return {
                CurrentRoot: element === document.documentElement && element.ownerDocument === document && element.isConnected,
                InternalError: uri.protocol === 'about:' && uri.pathname === 'neterror' && uri.hash === '',
                ErrorCode: uri.searchParams.getAll('e').length === 1 && uri.searchParams.get('e') === 'notCached',
                Target: uri.searchParams.getAll('u').length === 1 && uri.searchParams.get('u') === input.target,
                Location: document.location.href === input.target,
                Ready: document.readyState === 'complete',
                Secret: element.outerHTML.includes(input.seed),
                Recovery: document.querySelectorAll('.recovery-codes code').length !== 0
            };
        }
        """;
    private const string Prior = """
        (element, input) => element === document.documentElement && element.ownerDocument === document && element.isConnected &&
            document.documentURI === input.target && document.location.href === input.target &&
            document.readyState === 'complete' && !element.outerHTML.includes(input.seed) &&
            document.querySelectorAll('.recovery-codes code').length === 0
        """;
    private readonly IPage page;
    private readonly HistoryControlServer server;
    private readonly TaskCompletionSource firstCommit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IFrame frame;
    private long caught;
    private int commits;
    private int responses;
    private int commitsAtCatch;
    private bool priorVerified;

    /// <summary>Arms evidence before Back; only the original page is observed and no browser action is retried.</summary>
    internal HistoryDocumentEvidence(IPage page, HistoryControlServer server)
    {
        this.page = page;
        this.server = server;
        frame = page.MainFrame;
        page.FrameNavigated += Committed;
        page.Response += Response;
    }

    /// <summary>Positively identifies the fresh fixture's already-safe document before its sole history action.</summary>
    internal async Task RequirePriorAsync()
    {
        priorVerified = await HistoryDocumentRead.RunAsync(() => page.Locator(Root).EvaluateAsync<bool>(Prior,
            new
            {
                target = server.Origin + HistoryControlServer.SafePath,
                seed = HistoryControlServer.Canary
            },
            new()
            {
                Timeout = BudgetMilliseconds
            }), TimeSpan.FromMilliseconds(BudgetMilliseconds), () => page.CloseAsync(), TestContext.Current.CancellationToken);
        Assert.True(priorVerified);
        Assert.Equal(0, Volatile.Read(ref commits));
        Assert.Equal(0, Volatile.Read(ref responses));
    }

    /// <summary>Preserves the real classifier result or original exception after separately collecting finite document evidence.</summary>
    internal async Task<HistoryOutcome> ObserveAsync(HistoryNavigationEvidence navigation)
    {
        ExceptionDispatchInfo? original = null;
        var outcome = default(HistoryOutcome);
        try
        {
            outcome = await AdministratorHistory.ObserveAsync(page, HistoryControlServer.Canary,
                () => page.GoBackAsync(), (responseCount, commitCount) =>
                {
                    caught = Stopwatch.GetTimestamp();
                    navigation.Catch(responseCount, commitCount);
                    commitsAtCatch = Volatile.Read(ref commits);
                });
        }
        catch (Exception exception)
        {
            original = ExceptionDispatchInfo.Capture(exception);
        }
        ExceptionDispatchInfo? diagnostic = null;
        try
        {
            await RecordAsync();
        }
        catch (Exception exception) { diagnostic = ExceptionDispatchInfo.Capture(exception); }
        original?.Throw();
        diagnostic?.Throw();
        return outcome;
    }

    /// <summary>Shares one post-catch budget across event arrival and one atomic read; unavailable evidence never changes acceptance.</summary>
    private async Task RecordAsync()
    {
        var category = HistoryDocumentCategory.NoCacheMiss;
        DocumentProjection? document = null;
        ExceptionDispatchInfo? cleanupFailure = null;
        var before = 0;
        try
        {
            if (caught != 0)
            {
                await firstCommit.Task.WaitAsync(TimeSpan.FromMilliseconds(Remaining()), TestContext.Current.CancellationToken);
                before = Volatile.Read(ref commits);
                document = await HistoryDocumentRead.RunAsync(() => page.Locator(Root).EvaluateAsync<DocumentProjection>(Projection,
                    new
                    {
                        target = server.Origin + PostPath,
                        seed = HistoryControlServer.Canary
                    },
                    new()
                    {
                        Timeout = Remaining()
                    }), TimeSpan.FromMilliseconds(Remaining()), () => page.CloseAsync(), TestContext.Current.CancellationToken);
                category = Remaining() > 0 ? HistoryDocumentCategory.Read : HistoryDocumentCategory.Timeout;
            }
        }
        catch (TimeoutException) { category = HistoryDocumentCategory.Timeout; }
        catch (OperationCanceledException) { category = HistoryDocumentCategory.Cancelled; }
        catch (HistoryDocumentCleanupException exception)
        {
            category = HistoryDocumentCategory.CleanupIncomplete;
            cleanupFailure = ExceptionDispatchInfo.Capture(exception);
        }
        catch (PlaywrightException) { category = HistoryDocumentCategory.Unavailable; }
        catch (Exception) { category = HistoryDocumentCategory.Unexpected; }
        Write(new
        {
            category = category.ToString(),
            prior_verified = priorVerified,
            commits_at_catch = commitsAtCatch,
            commits_before_read = Math.Clamp(before, 0, CountLimit),
            commits_after_read = Math.Clamp(Volatile.Read(ref commits), 0, CountLimit),
            responses = Math.Clamp(Volatile.Read(ref responses), 0, CountLimit),
            same_frame = frame == page.MainFrame,
            posts = Math.Clamp(server.Posts, 0, CountLimit),
            document
        });
        cleanupFailure?.Throw();
    }

    /// <summary>Refuses expired work before either wait or browser command; no zero timeout can disable a deadline.</summary>
    private float Remaining()
    {
        var remaining = BudgetMilliseconds - Stopwatch.GetElapsedTime(caught).TotalMilliseconds;
        return remaining > 0 ? (float)remaining : throw new TimeoutException();
    }

    /// <summary>Retains each main-frame notification and releases the one pending event wait.</summary>
    private void Committed(object? sender, IFrame value)
    {
        if (value == frame)
        {
            Interlocked.Increment(ref commits);
            firstCommit.TrySetResult();
        }
    }

    /// <summary>Counts real main-frame navigation responses through the document read.</summary>
    private void Response(object? sender, IResponse value)
    {
        if (value.Frame == frame && value.Request.IsNavigationRequest)
        {
            Interlocked.Increment(ref responses);
        }
    }

    /// <summary>Writes only fixed categories and booleans; missing output is incomplete and cannot mask the original exception.</summary>
    private static void Write(object value)
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(OutputPrefix + JsonSerializer.Serialize(value));
        }
        catch (Exception) { /* Preserve the original browser verdict. */ }
    }

    /// <summary>Detaches both diagnostic subscriptions before page disposal.</summary>
    public void Dispose()
    {
        page.FrameNavigated -= Committed;
        page.Response -= Response;
    }
}

/// <summary>Contains no addresses, markup or native error text from the observed document.</summary>
internal sealed record DocumentProjection(bool CurrentRoot, bool InternalError, bool ErrorCode, bool Target, bool Location, bool Ready, bool Secret, bool Recovery);

internal enum HistoryDocumentCategory
{
    NoCacheMiss, Read, Timeout, Cancelled, Unavailable, Unexpected, CleanupIncomplete
}
