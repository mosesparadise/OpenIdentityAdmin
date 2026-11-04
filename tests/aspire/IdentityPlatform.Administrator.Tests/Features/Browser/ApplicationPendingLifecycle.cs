using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates UI lifecycle/output races from persistence acceptance with the existing explicitly response-only upstream probe.</summary>
internal static class ApplicationPendingLifecycle
{
    private const string CleanupReport = "application-lifecycle cleanup requests={0} browser-completed={1} upstream-started={2} upstream-completed={3} response-present={4}";
    private const string Pause = "pause";
    private const string ReplaceSuffix = "/secret-replacements";
    private const string Api = "/api/v1/applications/";
    private const string Post = "POST";
    private const string Tag = "etag";
    private const int GateSeconds = 3;
    private const int SecretBytes = 32;
    private const int MutationCount = 1;
    private const int Success = 200;
    private const int Unauthorized = 401;
    private const int OriginalBudgetSeconds = 5;
    private const string UnknownCreate = "The application may have been created.";
    private const string UnknownReplacement = "The secret may have changed.";
    private const string ExpiredDisplay = "This display has expired.";
    private const string ExpiredSession = "Your session observation has ended.";
    private const string SessionCookie = "__Host-identity-admin-session";
    private const string ApplicationField = "application";
    private const string CredentialField = "credential";
    private const string ExpiresField = "expiresAt";
    private const string SessionCeiling = "async minimum => { const r=await fetch('/bff/session',{cache:'no-store',redirect:'error'}); const s=await r.json(); return r.status===200 && s.authenticated===true && Date.parse(s.expiresAt)>minimum; }";
    private const string Logout = """
        async () => {
          const formResponse = await fetch('/bff/logout', {cache:'no-store',credentials:'same-origin',redirect:'error'});
          if(formResponse.status!==200) return false;
          const html = new DOMParser().parseFromString(await formResponse.text(),'text/html');
          const fields = html.querySelectorAll('form[action="/bff/logout"] input[type=hidden]');
          if(fields.length!==1 || fields[0].name!=='__RequestVerificationToken') return false;
          const body = new URLSearchParams(); body.set(fields[0].name,fields[0].value); body.set('Mode','local');
          const response = await fetch('/bff/logout',{method:'POST',credentials:'same-origin',cache:'no-store',redirect:'error',
            headers:{'Content-Type':'application/x-www-form-urlencoded'},body});
          return response.status===200 && response.headers.get('Cache-Control')==='no-store' &&
            (await response.text()).includes('This administrator session has ended locally.');
        }
        """;

    /// <summary>Starts one freshly authenticated fixture per boundary, so earlier cancellation cannot satisfy a later case.</summary>
    internal static async Task<string[]> RunAsync(BootstrapFixture fixture, string provider, ApplicationPendingBoundary boundary)
    {
        var progress = new ApplicationLifecycleProgress(ApplicationLifecycleScenario.Pending, boundary);
        progress.Mark(ApplicationLifecycleStage.Start);
        var probe = new ApplicationMutationProbe();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, mutationProbe: probe);
        using var environmentDisposal = progress.BeforeDispose(ApplicationLifecycleDisposal.Environment);
        progress.Mark(ApplicationLifecycleStage.EnvironmentReady);
        await using var browser = await BrowserSession.CreateHeadedAsync();
        using var browserDisposal = progress.BeforeDispose(ApplicationLifecycleDisposal.Browser);
        progress.Mark(ApplicationLifecycleStage.BrowserReady);
        var page = await browser.Context.NewPageAsync();
        var access = await ApplicationAccessFixture.SignInAsync(environment, page);
        progress.Mark(ApplicationLifecycleStage.SignedIn);
        var visibility = await ApplicationNativeVisibility.CreateAsync(page);
        progress.Mark(ApplicationLifecycleStage.VisibilityReady);
        var now = environment.Issuer.Account.Journey.Clock.Now;
        await page.Clock.SetFixedTimeAsync(now.UtcDateTime);
        probe.Access = access.Value;
        probe.Now = now;
        await using var observation = await ApplicationLifecycleObservation.CreateAsync(page, nativeTransitions: true);
        using var observationDisposal = progress.BeforeDispose(ApplicationLifecycleDisposal.Observation);
        progress.Mark(ApplicationLifecycleStage.ObserverReady);
        await ApplicationLifecycleUi.PrepareAsync(page, environment.Profile.ClientUri, now, ApplicationLifecycleUi.ShortLifetimeSeconds);
        progress.Mark(ApplicationLifecycleStage.Prepared);
        var replacement = boundary == ApplicationPendingBoundary.OriginalDeadline;
        if (replacement)
        {
            await PrepareReplacementAsync(page, probe, now);
        }

        await ExerciseAsync(page, visibility, environment, probe, observation, boundary, now, progress);
        var secret = Convert.ToBase64String(new byte[SecretBytes]);
        Assert.Equal(0, await page.Locator(ApplicationLifecycleUi.Secret).CountAsync());
        await observation.RequireAsync([secret, access.Value]);
        await BrowserSecretOracle.StorageAsync(page, [secret, access.Value]);
        if (boundary == ApplicationPendingBoundary.OriginalDeadline)
        {
            await EditorRecoveryView.PresentationAsync(page);
        }
        progress.Mark(ApplicationLifecycleStage.PrivacyChecked);
        return [secret, access.Value];
    }

    /// <summary>Retains a short original display observation while the later replacement response and session have longer valid lifetimes.</summary>
    private static async Task PrepareReplacementAsync(IPage page, ApplicationMutationProbe probe, DateTimeOffset now)
    {
        var response = await page.RunAndWaitForResponseAsync(() => ApplicationLifecycleUi.CreateAsync(page),
            value => new Uri(value.Url).AbsolutePath == ApplicationLifecycleUi.Collection && value.Request.Method == Post);
        Assert.True(response.Headers.TryGetValue(Tag, out var version));
        using var body = JsonDocument.Parse(await response.TextAsync());
        var original = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() + ApplicationLifecycleUi.ShortLifetimeSeconds);
        Assert.True(body.RootElement.GetProperty(ApplicationField).GetProperty(CredentialField).GetProperty(ExpiresField).GetDateTimeOffset() == original);
        Assert.True(await page.EvaluateAsync<bool>(SessionCeiling, original.ToUnixTimeMilliseconds()));
        probe.ExpectedPath = Api + ApplicationMutationProbe.Id + ReplaceSuffix;
        probe.ExpectedVersion = version;
        await ApplicationLifecycleUi.ReplacementAsync(page, now);
    }

    /// <summary>Requires an observed pending downstream send, applies one original boundary, and releases the gate on every exit.</summary>
    private static async Task ExerciseAsync(IPage page, ApplicationNativeVisibility visibility, BrowserHttpsFixture environment, ApplicationMutationProbe probe,
        ApplicationLifecycleObservation observation, ApplicationPendingBoundary boundary, DateTimeOffset now, ApplicationLifecycleProgress progress)
    {
        probe.Fault = Pause;
        var before = probe.Requests;
        var started = Stopwatch.GetTimestamp();
        using var write = new ApplicationLifecycleWrite(page);
        Exception? failure = null;
        try
        {
            await ApplicationLifecycleUi.Button(page, boundary == ApplicationPendingBoundary.OriginalDeadline ? ApplicationLifecycleUi.Replace : ApplicationLifecycleUi.Create).ClickAsync();
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(GateSeconds), TestContext.Current.CancellationToken);
            Assert.Equal(before + MutationCount, probe.Requests);
            Assert.False(probe.Canceled.Task.IsCompleted);
            progress.Mark(ApplicationLifecycleStage.Boundary);
            await BoundaryAsync(page, visibility, environment, probe, observation, boundary, now);
            progress.Mark(ApplicationLifecycleStage.BoundaryComplete);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(OriginalBudgetSeconds));
        }
        catch (Exception exception) { failure = exception; }
        progress.Mark(ApplicationLifecycleStage.Release);
        await CompleteAsync(probe, write, failure);
        progress.Mark(ApplicationLifecycleStage.RequestsJoined);
        Assert.Equal(MutationCount, write.Requests);
        if (boundary is ApplicationPendingBoundary.Session or ApplicationPendingBoundary.OriginalDeadline)
        {
            Assert.NotNull(write.Response);
            await RequireResultAsync(write.Response, boundary, now);
        }
        if (boundary != ApplicationPendingBoundary.Navigation)
        {
            await ApplicationLifecycleUi.HeadingAsync(page, ApplicationLifecycleUi.Problem);
            var message = boundary == ApplicationPendingBoundary.OriginalDeadline ? UnknownReplacement : UnknownCreate;
            Assert.True(await page.GetByText(message, new()
            {
                Exact = true
            }).IsVisibleAsync());
            if (boundary is ApplicationPendingBoundary.Session or ApplicationPendingBoundary.OriginalDeadline)
            {
                Assert.True(await page.GetByText(boundary == ApplicationPendingBoundary.Session ? ExpiredSession : ExpiredDisplay, new()
                {
                    Exact = false
                }).IsVisibleAsync());
            }
        }
        Assert.Equal(before + MutationCount, probe.Requests);
        Assert.True(probe.SafeRequest);
        Assert.Equal(0, probe.Redirects);
        progress.Mark(ApplicationLifecycleStage.ResultChecked);
    }

    /// <summary>Releases once and joins native browser and upstream work under one cleanup budget without replacing a primary failure.</summary>
    private static async Task CompleteAsync(ApplicationMutationProbe probe, ApplicationLifecycleWrite write, Exception? failure)
    {
        probe.Release.TrySetResult();
        try
        {
            var upstream = probe.Started.Task.IsCompleted ? probe.Completed.Task : Task.CompletedTask;
            var browser = write.Requests > 0 ? write.JoinAsync() : Task.CompletedTask;
            await Task.WhenAll(upstream, browser).WaitAsync(TimeSpan.FromSeconds(GateSeconds));
        }
        catch (Exception cleanup)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(CleanupReport, write.Requests, write.IsCompleted, probe.Started.Task.IsCompleted, probe.Completed.Task.IsCompleted, write.Response is not null);
            failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>Separates actual BFF session denial from a successful still-current response rejected solely by the older browser display ceiling.</summary>
    private static async Task RequireResultAsync(IResponse response, ApplicationPendingBoundary boundary, DateTimeOffset now)
    {
        ApplicationLifecycleUi.RequireNoStore(response);
        Assert.Equal(boundary == ApplicationPendingBoundary.Session ? Unauthorized : Success, response.Status);
        if (boundary == ApplicationPendingBoundary.OriginalDeadline)
        {
            using var body = JsonDocument.Parse(await response.TextAsync());
            var details = body.RootElement.GetProperty(ApplicationField);
            var original = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() + ApplicationLifecycleUi.ShortLifetimeSeconds);
            Assert.True(details.GetProperty(ExpiresField).GetDateTimeOffset() > original);
            Assert.True(details.GetProperty(CredentialField).GetProperty(ExpiresField).GetDateTimeOffset() > original);
        }
    }

    /// <summary>Uses real navigation, window minimization or native logout; only the documented deadline case advances the browser clock.</summary>
    private static async Task BoundaryAsync(IPage page, ApplicationNativeVisibility visibility, BrowserHttpsFixture environment, ApplicationMutationProbe probe,
        ApplicationLifecycleObservation observation, ApplicationPendingBoundary boundary, DateTimeOffset now)
    {
        switch (boundary)
        {
            case ApplicationPendingBoundary.Navigation:
                await ApplicationOutputLifecycle.HistoryAsync(page, environment.Profile.ClientUri, observation);
                break;
            case ApplicationPendingBoundary.Visibility:
                await ApplicationOutputLifecycle.HideAsync(page, visibility, observation);
                break;
            case ApplicationPendingBoundary.Session:
                Assert.True(await page.EvaluateAsync<bool>(Logout));
                Assert.DoesNotContain(await page.Context.CookiesAsync(), value => value.Name == SessionCookie);
                Assert.False(probe.Canceled.Task.IsCompleted);
                break;
            case ApplicationPendingBoundary.OriginalDeadline:
                var expires = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() + ApplicationLifecycleUi.ShortLifetimeSeconds);
                await page.Clock.SetFixedTimeAsync(expires.UtcDateTime);
                Assert.False(probe.Canceled.Task.IsCompleted);
                break;
        }
    }
}

/// <summary>Separates native lifecycle invalidation, durable local sign-out and the immutable original editor display ceiling.</summary>
internal enum ApplicationPendingBoundary
{
    Navigation, Visibility, Session, OriginalDeadline
}
