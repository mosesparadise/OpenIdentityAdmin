using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Observes Chromium's native terminal signal before Playwright's request-to-page lookup, without interception or wire capture.</summary>
internal sealed class ApplicationNativeNetwork
{
    private const string FrameTreeCommand = "Page.getFrameTree";
    private const string EnableCommand = "Network.enable";
    private const string StartedEvent = "Network.requestWillBeSent";
    private const string FinishedEvent = "Network.loadingFinished";
    private const string FailedEvent = "Network.loadingFailed";
    private const string FrameTree = "frameTree";
    private const string Frame = "frame";
    private const string Id = "id";
    private const string Loader = "loaderId";
    private const string FrameId = "frameId";
    private const string RequestId = "requestId";
    private const string Request = "request";
    private const string Url = "url";
    private const string Method = "method";
    private const string Redirect = "redirectResponse";
    private const int DetachSeconds = 3;
    private const string Incomplete = "Native request evidence was invalid at final observation sealing.";
    private const string CommandLine = "Browser.getBrowserCommandLine";
    private const string Arguments = "arguments";
    private const string DisableBackForwardCache = "--disable-back-forward-cache";
    private readonly ICDPSession session;
    private readonly string originalFrame;
    private readonly string originalLoader;
    internal ApplicationDocumentLifetime? Document
    {
        get; private set;
    }
    internal ApplicationNetworkLedger Ledger
    {
        get;
    }
    private bool closed;

    /// <summary>Attaches one isolated session to one already-loaded main frame before any observed submission.</summary>
    private ApplicationNativeNetwork(ICDPSession session, ApplicationNetworkLedger ledger, string frame, string loader)
    {
        this.session = session;
        originalFrame = frame;
        originalLoader = loader;
        Ledger = ledger;
        session.Event(StartedEvent).OnEvent += Started;
        session.Event(FinishedEvent).OnEvent += Finished;
        session.Event(FailedEvent).OnEvent += Failed;
        session.Close += Closed;
    }

    /// <summary>Reads the actual original frame/loader and enables observation only; no response, cache or network policy is changed.</summary>
    internal static async Task<ApplicationNativeNetwork> CreateAsync(IPage page, Uri target, bool navigation)
    {
        ApplicationNetworkControls.Require();
        var session = await page.Context.NewCDPSessionAsync(page);
        ApplicationNativeNetwork? owner = null;
        try
        {
            var value = await session.SendAsync(FrameTreeCommand);
            var frame = value!.Value.GetProperty(FrameTree).GetProperty(Frame);
            var id = Text(frame, Id);
            var loader = Text(frame, Loader);
            Assert.True(ApplicationNetworkLedger.Identifier(id) && ApplicationNetworkLedger.Identifier(loader));
            owner = new(session, new(id!, loader!, target.AbsoluteUri), id!, loader!);
            if (navigation)
            {
                await owner.ObserveDocumentAsync(id!);
            }
            await session.SendAsync(EnableCommand);
            return owner;
        }
        catch (Exception primary)
        {
            try
            {
                if (owner is not null)
                {
                    await owner.ReleaseAsync(requireComplete: false);
                }
                else
                {
                    await session.DetachAsync().WaitAsync(TimeSpan.FromSeconds(DetachSeconds));
                }
            }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    /// <summary>Verifies actual pinned browser launch policy before arming the original document on the same stable session.</summary>
    private async Task ObserveDocumentAsync(string frame)
    {
        ApplicationDocumentControls.Require();
        var commandLine = await session.SendAsync(CommandLine);
        var cacheDisabled = commandLine!.Value.GetProperty(Arguments).EnumerateArray().Any(argument => argument.GetString() == DisableBackForwardCache);
        Assert.True(cacheDisabled);
        Document = new(session, frame);
        await Document.EnableAsync();
    }

    /// <summary>Corroborates explicit context-epoch loss with the actual returned main frame's distinct loader under the remaining original interaction budget.</summary>
    internal async Task RequireNavigationAsync(TimeSpan remaining)
    {
        Assert.NotNull(Document);
        var started = Stopwatch.GetTimestamp();
        await Document.Ledger.Destroyed.WaitAsync(remaining);
        var budget = remaining - Stopwatch.GetElapsedTime(started);
        Assert.True(budget > TimeSpan.Zero);
        var value = await session.SendAsync(FrameTreeCommand).WaitAsync(budget);
        var frame = value!.Value.GetProperty(FrameTree).GetProperty(Frame);
        var loader = Text(frame, Loader);
        Assert.True(Text(frame, Id) == originalFrame && ApplicationNetworkLedger.Identifier(loader) && loader != originalLoader);
        Assert.True(Document.Ledger.IsDestroyed);
    }

    /// <summary>Reads only correlation fields and discards the event payload, including every header and body.</summary>
    private void Started(object? sender, JsonElement? message)
    {
        if (message is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(Request, out var request) || request.ValueKind != JsonValueKind.Object)
        {
            Ledger.Invalidate();
            return;
        }
        Ledger.Started(Text(value, RequestId), Text(value, FrameId), Text(value, Loader), Text(request, Url), Text(request, Method), value.TryGetProperty(Redirect, out _));
    }

    /// <summary>Accepts only the original native request's finish signal.</summary>
    private void Finished(object? sender, JsonElement? message) => Ledger.Ended(message is { } value ? Text(value, RequestId) : null, false);
    /// <summary>Accepts only the original native request's failure signal and never retains the native error text.</summary>
    private void Failed(object? sender, JsonElement? message) => Ledger.Ended(message is { } value ? Text(value, RequestId) : null, true);
    /// <summary>Session closure is a failure of observation, never a substitute for request completion.</summary>
    private void Closed(object? sender, ICDPSession value)
    {
        closed = true;
        Ledger.Invalidate();
        Document?.Ledger.Invalidate();
    }
    /// <summary>Reads only a named string field; malformed payloads cannot become correlated evidence.</summary>
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    /// <summary>Releases the separate observation session after request joining; its disposal budget cannot renew network completion.</summary>
    internal async Task ReleaseAsync(bool requireComplete = true)
    {
        session.Event(StartedEvent).OnEvent -= Started;
        session.Event(FinishedEvent).OnEvent -= Finished;
        session.Event(FailedEvent).OnEvent -= Failed;
        session.Close -= Closed;
        Document?.Freeze();
        Exception? failure = null;
        try
        {
            if (!closed)
            {
                await session.DetachAsync().WaitAsync(TimeSpan.FromSeconds(DetachSeconds));
            }
        }
        catch (Exception error) { failure = error; }
        var valid = !requireComplete || Ledger.Seal(requireTerminal: Document is null);
        if (requireComplete && Document is not null)
        {
            valid &= Document.Ledger.Seal();
        }
        if (!valid)
        {
            var invalid = new Xunit.Sdk.XunitException(Incomplete);
            failure = failure is null ? invalid : new AggregateException(failure, invalid);
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
