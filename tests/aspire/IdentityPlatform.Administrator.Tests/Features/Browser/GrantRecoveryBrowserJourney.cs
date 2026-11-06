using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Recovers real lost acceptance and stopped work through shipped React controls, exact HTTP bindings and independent durable evidence.</summary>
internal static class GrantRecoveryBrowserJourney
{
    private const string Collection = "/bff/api/v1/applications/";
    private const string Suffix = "/grant-revocations";
    private const string ContinueSuffix = "/continue";
    private const int LostAttempts = 3;
    private const int CompletedRevision = 3;
    private const int RecoveredApprovals = 3;
    private const int RecoveredRevision = 7;
    private const string Enter = "Enter";
    private const string Status = "Revocation status:";
    private const string Separator = "/";

    /// <summary>Drops only an actual committed response, retains uncertainty across real permission denial and explicitly recovers the same intent before Worker completion.</summary>
    internal static async Task LostAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string provider)
    {
        var native = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        var configuration = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        var url = Address(environment, registration.Id);
        using var traffic = new GrantRecoveryTraffic(page, url);
        await GrantRecoveryView.OpenAsync(page, environment.Profile.ClientUri, registration);
        await GrantRecoveryView.PrepareAsync(page, continuation: false);
        await using (var loss = await EditorRecoveryResponse.AttachAsync(page, url, GrantRecoveryTraffic.Post,
            EditorRecoveryResponse.Behavior.DropCommitted, committedStatus: GrantRecoveryView.Accepted))
        {
            await GrantRecoveryView.SendLostAsync(page);
            await GrantRecoveryView.MessageAsync(page, GrantRecoveryView.Unknown);
            loss.RequireHealthy();
            Assert.Equal(GrantRecoveryView.Accepted, loss.CommittedStatus);
            Assert.Equal(1, loss.Sends);
            Assert.Null(loss.SuppressedSecret);
        }
        var accepted = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        Assert.Single(accepted.Approvals);
        Assert.Single(accepted.Details);
        var original = Assert.Single(traffic.Posts);
        Assert.Equal(accepted.Approvals[0].RequestKey, original.Key);
        GrantRecoveryView.Require(original, url, ApplicationVersion.Create(registration.Id, configuration.Policy.Revision));
        await RecoverAsync(page, environment, registration.Id, url, traffic, original);
        await GrantRecoveryBrowserStore.UnchangedAsync(environment, registration.Id, accepted);
        await GrantRecoveryBrowserStore.CompleteAsync(page, environment, provider);
        var completed = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        completed.Preserves(accepted);
        Assert.Equal(GrantJobState.Succeeded, completed.Job.State);
        Assert.Equal(CompletedRevision, completed.Job.Revision);
        Assert.Single(completed.Approvals);
        Assert.Equal(LostAttempts, traffic.Posts.Length);
        await GrantRecoveryBrowserStore.RequireNativeAsync(environment, registration.Id, native, configuration);
        await GrantRecoveryView.PresentationAsync(page);
        await BrowserSecretOracle.StorageAsync(page, registration.Secrets);
    }

    /// <summary>A real denied recovery retains the exact intent; restored access explicitly replays it and then reads current protected progress.</summary>
    private static async Task RecoverAsync(IPage page, BrowserHttpsFixture environment, string application, string url,
        GrantRecoveryTraffic traffic, GrantRecoveryIntent original)
    {
        var before = await GrantRecoveryBrowserStore.ReadAsync(environment, application);
        await EditorRecoveryStore.WithoutPermissionAsync(environment, GrantRecoveryBrowserStore.Permission, async () =>
        {
            var denied = await page.RunAndWaitForResponseAsync(() => GrantRecoveryView.Button(page, GrantRecoveryView.Recover).ClickAsync(),
                response => response.Url == url && response.Request.Method == GrantRecoveryTraffic.Post);
            Assert.Equal(GrantRecoveryView.Denied, denied.Status);
            await GrantRecoveryView.MessageAsync(page, GrantRecoveryView.Forbidden);
            await GrantRecoveryView.MessageAsync(page, GrantRecoveryView.Unknown);
            await GrantRecoveryView.HiddenProgressAsync(page);
            await GrantRecoveryBrowserStore.UnchangedAsync(environment, application, before);
            await GrantRecoveryView.PresentationAsync(page);
        });
        var recovered = await page.RunAndWaitForResponseAsync(() => GrantRecoveryView.Button(page, GrantRecoveryView.Recover).PressAsync(Enter),
            response => response.Url == url && response.Request.Method == GrantRecoveryTraffic.Post);
        Assert.Equal(GrantRecoveryView.Accepted, recovered.Status);
        await Assertions.Expect(page.GetByText(Status, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
        Assert.True(traffic.Reads > 1);
        Assert.Equal(LostAttempts, traffic.Posts.Length);
        Assert.All(traffic.Posts, value => Assert.True(original == value));
    }

    /// <summary>Shows truthful partial progress, obtains a real authority stop, rejects a stale competing continuation and accepts only fresh explicit confirmation.</summary>
    internal static async Task StoppedAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string provider)
    {
        var native = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        var configuration = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        var url = Address(environment, registration.Id);
        using var traffic = new GrantRecoveryTraffic(page, url);
        await GrantRecoveryView.OpenAsync(page, environment.Profile.ClientUri, registration);
        await GrantRecoveryView.PrepareAsync(page, continuation: false);
        Assert.Equal(GrantRecoveryView.Accepted, (await GrantRecoveryView.SubmitAsync(page, url, continuation: false)).Status);
        var accepted = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        Assert.True(await GrantRecoveryBrowserStore.BatchAsync(environment, accepted.Job.Id));
        var stopped = await GrantRecoveryBrowserStore.StopAsync(environment, registration.Id, accepted.Job.Id);
        var continuation = url + Separator + accepted.Job.Id + ContinueSuffix;
        await StaleAsync(page, environment, registration, continuation, stopped.Job.Id, stopped.Job.Revision);
        var before = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        await GrantRecoveryView.OpenAsync(page, environment.Profile.ClientUri, registration);
        await GrantRecoveryView.StoppedAsync(page, before.Job.Id);
        await GrantRecoveryView.PrepareAsync(page, continuation: true, validation: true);
        await GrantRecoveryView.PresentationAsync(page);
        Assert.Equal(GrantRecoveryView.Accepted, (await GrantRecoveryView.SubmitAsync(page, continuation, continuation: true)).Status);
        var sent = traffic.Posts;
        Assert.Equal(LostAttempts, sent.Length);
        Assert.Equal(sent.Length, sent.Select(value => value.Key).Distinct().Count());
        GrantRecoveryView.Require(sent[^1], continuation, ApplicationVersion.Create(registration.Id, configuration.Policy.Revision), ApplicationVersion.Create(accepted.Job.Id.ToString(), before.Job.Revision));
        await GrantRecoveryBrowserStore.CompleteAsync(page, environment, provider);
        var completed = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        completed.Preserves(before);
        Assert.Equal(accepted.Job.Id, completed.Job.Id);
        Assert.Equal(GrantJobState.Succeeded, completed.Job.State);
        Assert.Equal(RecoveredApprovals, completed.Approvals.Length);
        Assert.Contains(completed.Approvals, value => value.RequestKey == sent[^1].Key);
        Assert.DoesNotContain(completed.Approvals, value => value.RequestKey == sent[1].Key);
        Assert.Equal(RecoveredRevision, completed.Job.Revision);
        await GrantRecoveryBrowserStore.RequireNativeAsync(environment, registration.Id, native, configuration);
        await GrantRecoveryView.PresentationAsync(page);
    }

    /// <summary>A second genuine browser continuation advances the job, which stops again before the original stale form is submitted.</summary>
    private static async Task StaleAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string url, Guid job, long revision)
    {
        await GrantRecoveryView.OpenAsync(page, environment.Profile.ClientUri, registration);
        await GrantRecoveryView.StoppedAsync(page, job);
        await GrantRecoveryView.PrepareAsync(page, continuation: true);
        await using var concurrent = await ApplicationEditorJourney.ConcurrentAsync(page);
        var other = await concurrent.NewPageAsync();
        try
        {
            await GrantRecoveryView.OpenAsync(other, environment.Profile.ClientUri, registration);
            await GrantRecoveryView.PrepareAsync(other, continuation: true);
            Assert.Equal(GrantRecoveryView.Accepted, (await GrantRecoveryView.SubmitAsync(other, url, continuation: true)).Status);
            var queued = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
            var stopped = await GrantRecoveryBrowserStore.StopAsync(environment, registration.Id, queued.Job.Id);
            var response = await GrantRecoveryView.SubmitAsync(page, url, continuation: true);
            Assert.Equal(GrantRecoveryView.Conflict, response.Status);
            Assert.Equal(GrantRecoveryIntent.Digest(ApplicationVersion.Create(queued.Job.Id.ToString(), revision)), GrantRecoveryIntent.Read(response.Request).JobVersion);
            await GrantRecoveryView.MessageAsync(page, GrantRecoveryView.Stale);
            await GrantRecoveryBrowserStore.UnchangedAsync(environment, registration.Id, stopped);
            await GrantRecoveryView.PresentationAsync(page);
        }
        finally { await other.CloseAsync(); }
    }

    /// <summary>Uses one immutable application-specific BFF collection for request observation and explicit recovery.</summary>
    private static string Address(BrowserHttpsFixture environment, string id) => new Uri(environment.Profile.ClientUri, Collection + Uri.EscapeDataString(id) + Suffix).AbsoluteUri;
}
