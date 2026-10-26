using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Holds a genuine fixed-route Kestrel request or response before dispatch/publication and joins its original browser operation.</summary>
internal sealed class BrowserNavigationPause
{
    private const string Resume = "/bff/login/resume";
    private const string Authorize = "/connect/authorize";
    private const string Callback = "/callback";
    private const int ArrivalSeconds = 20;
    private readonly TaskCompletionSource<Uri> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private BrowserNavigationStage stage;
    private int armed;
    private int used;
    private int visits;
    internal int Visits => Volatile.Read(ref visits);

    /// <summary>Freezes one phase before starting navigation and always releases/joins the same real browser request after observation.</summary>
    internal async Task RunAsync(BrowserNavigationStage selected, Func<Task> navigate, Func<Uri, Task> observe)
    {
        Assert.Equal(0, Interlocked.Exchange(ref used, 1));
        stage = selected;
        Assert.Equal(0, Interlocked.Exchange(ref armed, 1));
        Task pending;
        try
        {
            pending = navigate();
        }
        catch
        {
            release.TrySetResult();
            Volatile.Write(ref armed, 0);
            throw;
        }
        ExceptionDispatchInfo? failure = null;
        try
        {
            var first = await Task.WhenAny(arrived.Task, pending).WaitAsync(TimeSpan.FromSeconds(ArrivalSeconds), TestContext.Current.CancellationToken);
            Assert.True(ReferenceEquals(first, arrived.Task));
            var actual = await arrived.Task;
            Assert.False(pending.IsCompleted);
            await observe(actual);
            Assert.Equal(1, Visits);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { release.TrySetResult(); }
        try
        {
            await pending;
        }
        catch (Exception error) when (failure is not null) { throw new AggregateException(failure.SourceException, error); }
        finally { Volatile.Write(ref armed, 0); }
        failure?.Throw();
    }

    /// <summary>Waits only at the independently named original GET boundary; no response, token, cookie or native handler is fabricated.</summary>
    internal async Task VisitAsync(HttpContext http, bool response)
    {
        if (Volatile.Read(ref armed) == 0 || response != (stage == BrowserNavigationStage.ResumeRedirect) || !HttpMethods.IsGet(http.Request.Method))
        {
            return;
        }
        var path = stage switch
        {
            BrowserNavigationStage.ResumeRequest or BrowserNavigationStage.ResumeRedirect => Resume,
            BrowserNavigationStage.Authorization => Authorize,
            _ => Callback
        };
        if (http.Request.Path != path)
        {
            return;
        }
        if (response)
        {
            Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        }
        Assert.Equal(1, Interlocked.Increment(ref visits));
        var actual = new UriBuilder(http.Request.Scheme, http.Request.Host.Host, http.Request.Host.Port ?? HttpsPort,
            http.Request.Path)
        {
            Query = http.Request.QueryString.Value
        }.Uri;
        arrived.TrySetResult(actual);
        await release.Task.WaitAsync(http.RequestAborted);
    }
    private const int HttpsPort = 443;
}

/// <summary>Identifies the fixed real HTTPS request or response boundary under observation.</summary>
public enum BrowserNavigationStage
{
    ResumeRequest, ResumeRedirect, Authorization, Callback
}
