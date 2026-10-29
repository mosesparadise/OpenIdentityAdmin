using System.Diagnostics;
using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates real caller-disconnect propagation from browser-engine Fetch promise semantics without extending either server or cleanup deadlines.</summary>
internal static class ApplicationProxyCancellation
{
    private const string Collection = "/bff/api/v1/applications";
    private const int CancellationSeconds = 3;
    private const int ServerDeadlineSeconds = 5;

    /// <summary>Uses only the actual host-bound session/generation cookies and proves a successful native transport baseline before caller cancellation.</summary>
    internal static async Task RequireAsync(BrowserHttpsFixture environment, IPage page, ApplicationProxyProbe probe)
    {
        var jar = new CookieContainer();
        foreach (var cookie in await BrowserHttpsJourney.CookiesAsync(environment, page))
        {
            jar.Add(environment.Profile.ClientUri, new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path)
            {
                Secure = true,
                HttpOnly = true
            });
        }
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = jar, AllowAutoRedirect = false });
        var uri = new Uri(environment.Profile.ClientUri, Collection);
        probe.Fault = ApplicationProxyFault.None;
        using var baseline = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        var before = await BrowserHttpsState.SnapshotAsync(environment);
        await RequireDisconnectAsync(client, uri, probe);
        Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
    }

    /// <summary>Observed downstream work cancels before the server deadline, with one send and a separately bounded caller-cancellation join.</summary>
    private static async Task RequireDisconnectAsync(HttpClient client, Uri uri, ApplicationProxyProbe probe)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        probe.BeginDelay();
        var calls = probe.Requests;
        var started = Stopwatch.GetTimestamp();
        var request = client.GetAsync(uri, caller.Token);
        try
        {
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(CancellationSeconds), TestContext.Current.CancellationToken);
            Assert.False(request.IsCompleted);
            var cancellation = Stopwatch.GetTimestamp();
            await caller.CancelAsync().WaitAsync(Remaining(cancellation), TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(Remaining(cancellation), TestContext.Current.CancellationToken));
            await probe.Canceled.Task.WaitAsync(Remaining(cancellation), TestContext.Current.CancellationToken);
            Assert.True(Stopwatch.GetElapsedTime(cancellation) < TimeSpan.FromSeconds(CancellationSeconds));
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(ServerDeadlineSeconds));
            Assert.Equal(calls + 1, probe.Requests);
            Assert.True(probe.CredentialsMatch);
        }
        finally
        {
            await caller.CancelAsync();
            try
            {
                using var response = await request;
            }
            catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
        }
    }

    /// <summary>Shares one original cancellation envelope across the caller callback, client task and exact downstream join.</summary>
    private static TimeSpan Remaining(long started)
    {
        var remaining = TimeSpan.FromSeconds(CancellationSeconds) - Stopwatch.GetElapsedTime(started);
        Assert.True(remaining > TimeSpan.Zero);
        return remaining;
    }

}
