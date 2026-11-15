using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises lost retirement output and durable stopped recovery without preserving or injecting transient browser intent.</summary>
internal static class RetirementBrowserJourney
{
    private const string Grants = "/grant-revocations";
    private const string Continue = "/continue";
    private const string Separator = "/";
    private const int ContinuedApprovals = 3;
    private const int BrowserPosts = 2;

    /// <summary>Drops one actually committed 202, reloads and signs out/in, then observes the exact retained job with no mutation replay.</summary>
    internal static async Task LostAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string provider)
    {
        var before = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        var native = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        var retirement = Address(environment, registration.Id, RetirementBrowserView.Suffix);
        using var traffic = new GrantRecoveryTraffic(page, retirement);
        using var grants = new GrantRecoveryTraffic(page, Address(environment, registration.Id, Grants));
        await RetirementBrowserView.PrepareAsync(page, environment, registration);
        await using (var loss = await EditorRecoveryResponse.AttachAsync(page, retirement, GrantRecoveryTraffic.Post,
            EditorRecoveryResponse.Behavior.DropCommitted, committedStatus: GrantRecoveryView.Accepted))
        {
            await RetirementBrowserView.SendAsync(page);
            await Assertions.Expect(page.GetByText(RetirementBrowserView.Unknown, new()
            {
                Exact = false
            })).ToBeVisibleAsync();
            loss.RequireHealthy();
            Assert.Equal(GrantRecoveryView.Accepted, loss.CommittedStatus);
            Assert.Equal(1, loss.Sends);
            Assert.Null(loss.SuppressedSecret);
        }
        var receipt = await RetirementBrowserStore.ReceiptAsync(environment, registration.Id);
        var accepted = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        GrantRecoveryView.Require(Assert.Single(traffic.Posts), retirement, ApplicationVersion.Create(registration.Id, before.Policy.Revision));
        Assert.Equal(receipt.RequestKey, traffic.Posts[0].Key);
        Assert.Single(accepted.Approvals);
        var signIn = await RetirementBrowserView.ReloginAsync(page, environment, registration, receipt.GrantJobId);
        await GrantRecoveryBrowserStore.UnchangedAsync(environment, registration.Id, accepted);
        var terminal = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        await GrantRecoveryBrowserStore.CompleteAsync(page, environment, provider, RetirementBrowserView.Completed);
        await RequireCompletionAsync(page, environment, registration, receipt.GrantJobId);
        Assert.Single(traffic.Posts);
        Assert.Empty(grants.Posts);
        Assert.True(grants.Reads > 1);
        Assert.Single((await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id)).Approvals);
        await RetirementBrowserStore.RequireAsync(environment, receipt, before);
        await GrantRecoveryBrowserStore.RequireNativeAsync(environment, registration.Id, [.. native, .. signIn], terminal);
    }

    /// <summary>Preserves partial progress through real permission loss and relogin, then rejects a stale form before a fresh explicit same-job continuation.</summary>
    internal static async Task StoppedAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string provider)
    {
        var before = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        var native = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        var retirement = Address(environment, registration.Id, RetirementBrowserView.Suffix);
        using var admission = new GrantRecoveryTraffic(page, retirement);
        using var traffic = new GrantRecoveryTraffic(page, Address(environment, registration.Id, Grants));
        await RetirementBrowserView.PrepareAsync(page, environment, registration);
        var response = await page.RunAndWaitForResponseAsync(() => RetirementBrowserView.SendAsync(page),
            value => value.Url == retirement && value.Request.Method == GrantRecoveryTraffic.Post);
        Assert.Equal(GrantRecoveryView.Accepted, response.Status);
        var receipt = await RetirementBrowserStore.ReceiptAsync(environment, registration.Id);
        Assert.True(await GrantRecoveryBrowserStore.BatchAsync(environment, receipt.GrantJobId));
        await GrantRecoveryBrowserStore.StopAsync(environment, registration.Id, receipt.GrantJobId);
        var signIn = await RetirementBrowserView.ReloginAsync(page, environment, registration, receipt.GrantJobId);
        await GrantRecoveryView.StoppedAsync(page, receipt.GrantJobId);
        var continuation = Address(environment, registration.Id, Grants) + Separator + receipt.GrantJobId + Continue;
        await StaleAsync(page, environment, registration, continuation, receipt.GrantJobId);
        var stopped = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        await RetirementBrowserView.OpenAsync(page, environment, registration, receipt.GrantJobId);
        await GrantRecoveryView.PrepareAsync(page, continuation: true, validation: true);
        Assert.Equal(GrantRecoveryView.Accepted, (await GrantRecoveryView.SubmitAsync(page, continuation, continuation: true)).Status);
        GrantRecoveryView.Require(traffic.Posts[^1], continuation, ApplicationVersion.Create(registration.Id, receipt.RetiredRevision),
            ApplicationVersion.Create(receipt.GrantJobId.ToString(), stopped.Job.Revision));
        var terminal = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, registration.Id);
        await GrantRecoveryBrowserStore.CompleteAsync(page, environment, provider, RetirementBrowserView.Completed);
        await RequireCompletionAsync(page, environment, registration, receipt.GrantJobId);
        var completed = await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id);
        completed.Preserves(stopped);
        Assert.Equal(ContinuedApprovals, completed.Approvals.Length);
        Assert.Single(admission.Posts);
        Assert.Equal(BrowserPosts, traffic.Posts.Length);
        Assert.NotEqual(traffic.Posts[0].Key, traffic.Posts[1].Key);
        Assert.DoesNotContain(completed.Approvals, row => row.RequestKey == traffic.Posts[0].Key);
        await RetirementBrowserStore.RequireAsync(environment, receipt, before);
        await GrantRecoveryBrowserStore.RequireNativeAsync(environment, registration.Id, [.. native, .. signIn], terminal);
    }

    /// <summary>Advances and stops the same job through a competing real browser while the original confirmation retains stale versions.</summary>
    private static async Task StaleAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string url, Guid job)
    {
        await GrantRecoveryView.PrepareAsync(page, continuation: true);
        await using var concurrent = await ApplicationEditorJourney.ConcurrentAsync(page);
        var other = await concurrent.NewPageAsync();
        try
        {
            await RetirementBrowserView.OpenAsync(other, environment, registration, job);
            await GrantRecoveryView.PrepareAsync(other, continuation: true);
            Assert.Equal(GrantRecoveryView.Accepted, (await GrantRecoveryView.SubmitAsync(other, url, continuation: true)).Status);
            var stopped = await GrantRecoveryBrowserStore.StopAsync(environment, registration.Id, job);
            Assert.Equal(GrantRecoveryView.Conflict, (await GrantRecoveryView.SubmitAsync(page, url, continuation: true)).Status);
            await GrantRecoveryView.MessageAsync(page, GrantRecoveryView.Stale);
            await GrantRecoveryBrowserStore.UnchangedAsync(environment, registration.Id, stopped);
        }
        finally { await other.CloseAsync(); }
    }

    /// <summary>Reloads completed terminal state and verifies there is no new-submit or ordinary mutation path, with retained theme/reflow/axe checks.</summary>
    private static async Task RequireCompletionAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, Guid job)
    {
        await RetirementBrowserView.OpenAsync(page, environment, registration, job);
        await Assertions.Expect(page.GetByText(RetirementBrowserView.Completed, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        Assert.Equal(GrantJobState.Succeeded, (await GrantRecoveryBrowserStore.ReadAsync(environment, registration.Id)).Job.State);
        await GrantRecoveryView.PresentationAsync(page);
        await BrowserSecretOracle.StorageAsync(page, registration.Secrets);
    }

    /// <summary>Builds a fixed same-origin observation address without browser state or request-key injection.</summary>
    private static string Address(BrowserHttpsFixture environment, string id, string suffix) =>
        new Uri(environment.Profile.ClientUri, RetirementBrowserView.Collection + Uri.EscapeDataString(id) + suffix).AbsoluteUri;
}
