using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins the exact browser mutation's native network completion without reading or logging credential-bearing wire bodies.</summary>
internal sealed class ApplicationLifecycleWrite : IDisposable
{
    private const string Post = "POST";
    private const int JoinSeconds = 3;
    private readonly IPage page;
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IRequest? request;
    internal bool IsCompleted => completed.Task.IsCompletedSuccessfully;
    internal IResponse? Response
    {
        get; private set;
    }
    internal int Requests
    {
        get; private set;
    }

    /// <summary>Attaches before the visible submit action and observes only the fixed application mutation route family.</summary>
    internal ApplicationLifecycleWrite(IPage page)
    {
        this.page = page;
        page.Request += Sent;
        page.Response += Received;
        page.RequestFinished += Ended;
        page.RequestFailed += Ended;
    }

    /// <summary>Retains the first actual browser request identity and counts every additional dispatch as a separate attempt.</summary>
    private void Sent(object? sender, IRequest value)
    {
        if (value.Method != Post || !new Uri(value.Url).AbsolutePath.StartsWith(ApplicationLifecycleUi.Collection, StringComparison.Ordinal))
        {
            return;
        }

        Requests++;
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

    /// <summary>Observes actual finish or abort; merely releasing the upstream gate does not finish this witness.</summary>
    private void Ended(object? sender, IRequest value)
    {
        if (ReferenceEquals(value, request))
        {
            completed.TrySetResult();
        }
    }

    /// <summary>Waits under an independent bounded cleanup budget for the original browser request.</summary>
    internal Task JoinAsync() => completed.Task.WaitAsync(TimeSpan.FromSeconds(JoinSeconds));

    /// <summary>Detaches only after the owner has explicitly joined cleanup and preserved any primary failure.</summary>
    public void Dispose()
    {
        page.Request -= Sent;
        page.Response -= Received;
        page.RequestFinished -= Ended;
        page.RequestFailed -= Ended;
    }
}
