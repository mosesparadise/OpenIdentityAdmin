using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves lifecycle stale-version and lost committed response recovery using actual UI, BFF/API and independently persisted rows.</summary>
internal static class LifecycleRecoveryBrowser
{
    private const string Disable = "Disable application";
    private const string Enable = "Re-enable application";
    private const string DisableSuffix = "/disable";
    private const string Reason = "#lifecycle-reason";
    private const string Confirm = "#lifecycle-confirm";
    private const string Maintenance = "maintenance";
    private const string ChangedDisplay = "Concurrent editor changed this registration";
    private const string Updated = "Application updated";
    private const string Stale = "This version is no longer current.";
    private const string Unknown = "The application status may have changed.";
    private const string Post = "POST";
    private const string DisabledEvent = "ApplicationDisabled";
    private const int Success = 200;
    private const int PreconditionFailed = 412;

    /// <summary>A concurrent real edit invalidates the screen; only explicit reload precedes one later lifecycle action whose committed response is lost.</summary>
    internal static async Task RequireAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        await PrepareAsync(environment, page);
        await using var concurrent = await ApplicationEditorJourney.ConcurrentAsync(page);
        var other = await concurrent.NewPageAsync();
        try
        {
            await EditorRecoveryView.OpenAsync(other, environment.Profile.ClientUri);
            await EditorRecoveryView.Button(other, EditorRecoveryView.Edit).ClickAsync();
            await EditorRecoveryView.HeadingAsync(other, EditorRecoveryView.Edit);
            await other.Locator(EditorRecoveryView.DisplayField).FillAsync(ChangedDisplay);
            await EditorRecoveryView.Button(other, EditorRecoveryView.Save).ClickAsync();
            await EditorRecoveryView.HeadingAsync(other, Updated);
        }
        finally { await other.CloseAsync(); }
        var before = await SnapshotAsync(environment, id);
        using var sends = new EditorRecoverySends(page);
        var url = EditorRecoveryFailures.Address(environment, id) + DisableSuffix;
        var denied = await page.RunAndWaitForResponseAsync(() => EditorRecoveryView.Button(page, Disable).ClickAsync(),
            response => response.Url == url && response.Request.Method == Post);
        Assert.Equal(PreconditionFailed, denied.Status);
        await EditorRecoveryView.ProblemAsync(page, Stale);
        Assert.Equal(1, sends.Count);
        Assert.Equal(before.Digest, (await SnapshotAsync(environment, id)).Digest);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Reload).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
        Assert.Equal(1, sends.Count);
        await LostAsync(environment, page, id);
    }

    /// <summary>Aborts only the response after real success; reload observes Disabled without any automatic repeat or secret output.</summary>
    private static async Task LostAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        await PrepareAsync(environment, page);
        var before = await SnapshotAsync(environment, id);
        using var sends = new EditorRecoverySends(page);
        await using (var probe = await EditorRecoveryResponse.AttachAsync(page, EditorRecoveryFailures.Address(environment, id) + DisableSuffix,
            Post, EditorRecoveryResponse.Behavior.DropCommitted))
        {
            await EditorRecoveryView.Button(page, Disable).ClickAsync();
            await EditorRecoveryView.ProblemAsync(page, Unknown);
            probe.RequireHealthy();
            Assert.Equal(1, probe.Sends);
            Assert.Equal(Success, probe.CommittedStatus);
            Assert.Null(probe.SuppressedSecret);
        }
        var after = await SnapshotAsync(environment, id);
        Assert.True(before.Policy.Enabled);
        Assert.False(after.Policy.Enabled);
        Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(before.Policy.CredentialRevision, after.Policy.CredentialRevision);
        Assert.Equal(before.Policy.CredentialExpiresAt, after.Policy.CredentialExpiresAt);
        var audit = Assert.Single(after.Audits, row => !before.Audits.Any(prior => prior.Id == row.Id));
        Assert.Equal(DisabledEvent, audit.Event);
        var reason = Assert.Single(after.LifecycleAudits, row => !before.LifecycleAudits.Any(prior => prior.AuditId == row.AuditId));
        Assert.Equal(audit.Id, reason.AuditId);
        Assert.Equal(1, (int)reason.ReasonCode);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Reload).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
        Assert.True(await EditorRecoveryView.Button(page, Enable).IsVisibleAsync());
        Assert.Equal(1, sends.Count);
        Assert.Equal(after.Digest, (await SnapshotAsync(environment, id)).Digest);
    }

    /// <summary>Explicitly confirms a new lifecycle draft against freshly read details; no failed draft is restored or resubmitted.</summary>
    private static async Task PrepareAsync(BrowserHttpsFixture environment, IPage page)
    {
        await EditorRecoveryView.OpenAsync(page, environment.Profile.ClientUri);
        await EditorRecoveryView.Button(page, Disable).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, Disable);
        Assert.False(await page.Locator(Confirm).IsCheckedAsync());
        await page.Locator(Reason).SelectOptionAsync(Maintenance);
        await page.Locator(Confirm).CheckAsync();
    }

    /// <summary>Reads complete detached application, reason and audit state after endpoint-owned transaction cleanup.</summary>
    private static async Task<ApplicationUpdateSnapshot> SnapshotAsync(BrowserHttpsFixture environment, string id)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        return await ApplicationUpdateSnapshot.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), id, TestContext.Current.CancellationToken);
    }
}
