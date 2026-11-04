using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates response-rendering witnesses from genuine permission denial, expiry and committed-response loss.</summary>
internal static class EditorRecoveryFailures
{
    private const int Success = 200;
    private const int Unauthorized = 401;
    private const string UncertainEdit = "Your changes may have been saved.";
    private const string Put = "PUT";
    private const string Post = "POST";
    private const string Get = "GET";
    private const string Separator = "/";
    private const string Replacement = "/secret-replacements";
    private const string Permission = "applications.write";
    private const string CredentialPermission = "applications.credentials";
    private const string Draft = "Draft that must not reappear";
    private const string Forbidden = "Your current permissions do not allow this operation.";
    private const string CredentialForbidden = "Secret replacement requires current credential permission and a recent MFA sign-in.";
    private const string Expired = "Your session observation has ended.";
    private const string SignIn = "Sign in";
    private const string Unavailable = "We couldn’t confirm the requested operation.";
    private const string Unknown = "The secret may have changed.";
    private const string NoEdit = "No edit request was sent by this operation.";
    private static readonly (int Status, string Message)[] Faults =
    [
        (400, "The server could not accept this request."),
        (404, "This registration could not be found."),
        (409, "The server reported a registration conflict."),
        (412, "This version is no longer current."),
        (428, "This version is no longer current."),
        (503, Unavailable)
    ];

    /// <summary>Renders each finite dispatched failure without claiming the response seam is an API denial oracle.</summary>
    internal static async Task FiniteAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        foreach (var fault in Faults)
        {
            await PrepareEditAsync(environment, page);
            using var sends = new EditorRecoverySends(page);
            await using (var probe = await EditorRecoveryResponse.AttachAsync(page, Address(environment, id), Put, status: fault.Status))
            {
                await EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync();
                await EditorRecoveryView.ProblemAsync(page, fault.Message);
                Assert.True(await page.GetByText(UncertainEdit, new()
                {
                    Exact = false
                }).IsVisibleAsync());
                Assert.Equal(1, probe.Sends);
                Assert.Equal(1, sends.Count);
                probe.RequireHealthy();
                Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
            }
            await EditorRecoveryView.Button(page, EditorRecoveryView.Reload).ClickAsync();
            await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
            await EditorRecoveryView.Button(page, EditorRecoveryView.Edit).ClickAsync();
            Assert.True(await page.Locator(EditorRecoveryView.DisplayField).InputValueAsync() == EditorRecoveryView.ChangedDisplay);
            await EditorRecoveryView.Button(page, EditorRecoveryView.Cancel).ClickAsync();
        }
        await PreflightAsync(environment, page, id);
    }

    /// <summary>Antiforgery acquisition failure is visibly unsent and preserves the actual native application and audit stamp.</summary>
    private static async Task PreflightAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        await PrepareEditAsync(environment, page);
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        using var sends = new EditorRecoverySends(page);
        await using var probe = await EditorRecoveryResponse.AttachAsync(page, new Uri(environment.Profile.ClientUri, EditorRecoveryView.Token).AbsoluteUri, Get);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync();
        await EditorRecoveryView.ProblemAsync(page, Unavailable);
        Assert.True(await page.GetByText(NoEdit, new()
        {
            Exact = false
        }).IsVisibleAsync());
        Assert.Equal(1, probe.Sends);
        Assert.Equal(0, sends.Count);
        probe.RequireHealthy();
        Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
    }

    /// <summary>Both mutation permissions are removed in the actual database while the reader retains permission to open details.</summary>
    internal static async Task PermissionsAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        await EditorRecoveryStore.WithoutPermissionAsync(environment, Permission, async () =>
        {
            await PrepareEditAsync(environment, page);
            using var sends = new EditorRecoverySends(page);
            await EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync();
            await EditorRecoveryView.ProblemAsync(page, Forbidden);
            Assert.Equal(1, sends.Count);
            Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
        });
        await EditorRecoveryStore.WithoutPermissionAsync(environment, CredentialPermission, async () =>
        {
            await PrepareReplacementAsync(environment, page);
            using var sends = new EditorRecoverySends(page);
            await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
            await EditorRecoveryView.ProblemAsync(page, CredentialForbidden);
            Assert.Equal(1, sends.Count);
            Assert.True(await page.GetByRole(AriaRole.Link, new()
            {
                Name = SignIn,
                Exact = true
            }).IsVisibleAsync());
            Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
        });
    }

    /// <summary>Loses an actual committed replacement response; explicit reload cannot recover its generated secret or repeat the mutation.</summary>
    internal static async Task<string> LostOutputAsync(BrowserHttpsFixture environment, IPage page, string id, string[] earlier)
    {
        await PrepareReplacementAsync(environment, page);
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        var probe = await EditorRecoveryResponse.AttachAsync(page, Address(environment, id) + Replacement, Post, EditorRecoveryResponse.Behavior.DropCommitted);
        await using (probe)
        {
            await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
            await EditorRecoveryView.ProblemAsync(page, Unknown);
            Assert.Equal(1, probe.Sends);
            probe.RequireHealthy();
            Assert.Equal(Success, probe.CommittedStatus);
            Assert.True(!string.IsNullOrEmpty(probe.SuppressedSecret));
        }
        var committed = await EditorRecoveryStore.ReadAsync(environment, id);
        Assert.Equal(before.Revision + 1, committed.Revision);
        Assert.Equal(before.AuditCount + 1, committed.AuditCount);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Reload).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
        Assert.Equal(committed, await EditorRecoveryStore.ReadAsync(environment, id));
        await BrowserSecretOracle.StorageAsync(page, [.. earlier, probe.SuppressedSecret!]);
        return probe.SuppressedSecret!;
    }

    /// <summary>Moves only the real server clock to original access expiry; browser Date stays earlier so this proves a real network denial.</summary>
    internal static async Task ExpiryAsync(BrowserHttpsFixture environment, IPage page, string id, DateTimeOffset expiry)
    {
        await PrepareEditAsync(environment, page);
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        environment.Issuer.Account.Journey.Clock.Now = expiry;
        using var sends = new EditorRecoverySends(page);
        var token = new Uri(environment.Profile.ClientUri, EditorRecoveryView.Token).AbsoluteUri;
        var response = await page.RunAndWaitForResponseAsync(() => EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync(),
            value => value.Url == token && value.Request.Method == Get);
        Assert.Equal(Unauthorized, response.Status);
        Assert.Equal(0, sends.Count);
        await EditorRecoveryView.ProblemAsync(page, Expired);
        Assert.True(await page.GetByRole(AriaRole.Link, new()
        {
            Name = SignIn,
            Exact = true
        }).IsVisibleAsync());
        Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
    }

    /// <summary>Opens current details and prepares one explicit bounded edit without changing immutable identity.</summary>
    private static async Task PrepareEditAsync(BrowserHttpsFixture environment, IPage page)
    {
        await EditorRecoveryView.OpenAsync(page, environment.Profile.ClientUri);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Edit).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Edit);
        await page.Locator(EditorRecoveryView.DisplayField).FillAsync(Draft);
    }

    /// <summary>Prepares one explicitly confirmed replacement against freshly observed details.</summary>
    private static async Task PrepareReplacementAsync(BrowserHttpsFixture environment, IPage page)
    {
        await EditorRecoveryView.OpenAsync(page, environment.Profile.ClientUri);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Replace);
        await EditorRecoveryView.FillExpiryAsync(page, environment.Issuer.Account.Journey.Clock.Now);
        await page.Locator(EditorRecoveryView.Confirmation).CheckAsync();
    }

    /// <summary>Builds only the exact opaque application path; route seams cannot match arbitrary downstream origins.</summary>
    internal static string Address(BrowserHttpsFixture environment, string id) => new Uri(environment.Profile.ClientUri,
        EditorRecoveryView.Collection + Separator + Uri.EscapeDataString(id)).AbsoluteUri;
}
