using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins the exact browser mutation's native network completion without reading or logging credential-bearing wire bodies.</summary>
internal sealed class ApplicationLifecycleWrite : IDisposable
{
    private const string Post = "POST";
    private const string Chromium = "chromium";
    private const int JoinSeconds = 3;
    private readonly IPage page;
    private readonly IBrowserContext context;
    private readonly ApplicationNativeNetwork? native;
    private readonly Uri target;
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IRequest? request;
    private bool matched = true;
    internal bool IsCompleted => completed.Task.IsCompletedSuccessfully;
    internal int NativeRequests => native?.Ledger.Requests ?? -1;
    internal bool NativeCompleted => native?.Ledger.IsComplete == true;
    internal bool NativeFailed => native?.Ledger.Failed == true;
    internal bool IsPageCompleted
    {
        get; private set;
    }
    internal IResponse? Response
    {
        get; private set;
    }
    internal int Requests
    {
        get; private set;
    }

    /// <summary>Attaches before the visible submit action and observes only the fixed application mutation route family.</summary>
    private ApplicationLifecycleWrite(IPage page, Uri target, ApplicationNativeNetwork? native)
    {
        this.page = page;
        this.target = target;
        this.native = native;
        context = page.Context;
        page.Request += Sent;
        page.RequestFinished += PageEnded;
        page.RequestFailed += PageEnded;
        context.Response += Received;
        context.RequestFinished += Ended;
        context.RequestFailed += Ended;
    }

    /// <summary>Arms Chromium's raw native observation before submission while retaining the original Playwright request and context events.</summary>
    internal static async Task<ApplicationLifecycleWrite> CreateAsync(IPage page, Uri target)
    {
        Assert.True(target.Scheme == Uri.UriSchemeHttps && target.Query.Length == 0 && target.AbsolutePath.StartsWith(ApplicationLifecycleUi.Collection, StringComparison.Ordinal));
        var native = BrowserSession.Engine == Chromium ? await ApplicationNativeNetwork.CreateAsync(page, target) : null;
        return new(page, target, native);
    }

    /// <summary>Retains the first actual browser request identity and counts every additional dispatch as a separate attempt.</summary>
    private void Sent(object? sender, IRequest value)
    {
        if (value.Method != Post || !new Uri(value.Url).AbsolutePath.StartsWith(ApplicationLifecycleUi.Collection, StringComparison.Ordinal))
        {
            return;
        }

        Requests++;
        matched &= value.Url == target.AbsoluteUri && ReferenceEquals(value.Frame, page.MainFrame);
        request ??= value;
    }

    /// <summary>Retains only the exact original request's native response for safe status and header assertions.</summary>
    private void Received(object? sender, IResponse value)
    {
        if (ReferenceEquals(value.Request, request))
        {
            Response = value;
        }
    }

    /// <summary>Observes native finish or abort even when navigation removes the event's optional page association; upstream release cannot finish this witness.</summary>
    private void Ended(object? sender, IRequest value)
    {
        if (ReferenceEquals(value, request))
        {
            completed.TrySetResult();
        }
    }

    /// <summary>Separately records the optional page-associated notification without substituting for native context completion.</summary>
    private void PageEnded(object? sender, IRequest value)
    {
        if (ReferenceEquals(value, request))
        {
            IsPageCompleted = true;
        }
    }

    /// <summary>Waits under an independent bounded cleanup budget for the original browser request.</summary>
    internal async Task JoinAsync()
    {
        await (native?.Ledger.Completion ?? completed.Task).WaitAsync(TimeSpan.FromSeconds(JoinSeconds));
        Assert.True(native?.Ledger.IsComplete ?? IsCompleted);
        Assert.True(matched && request is not null && Requests == 1);
    }

    /// <summary>Separately releases the native observation session after joining; it cannot stand in for missing network completion.</summary>
    internal Task ReleaseNativeAsync() => native?.ReleaseAsync() ?? Task.CompletedTask;

    /// <summary>Detaches only after the owner has explicitly joined cleanup and preserved any primary failure.</summary>
    public void Dispose()
    {
        page.Request -= Sent;
        page.RequestFinished -= PageEnded;
        page.RequestFailed -= PageEnded;
        context.Response -= Received;
        context.RequestFinished -= Ended;
        context.RequestFailed -= Ended;
    }
}
