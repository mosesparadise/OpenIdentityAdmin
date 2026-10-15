using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves history observations wait for readiness without waiting away exposed secret material.</summary>
[Trait("Category", "Browser")]
[Trait("BrowserStory", "Accounts")]
public sealed class HistoryObservationTests
{
    private const string Canary = "synthetic-history-canary";
    private const string Failure = "Committed account secrets reappeared during browser history traversal.";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string Prefix = "<!doctype html><html><body>";
    private const string Suffix = "</body></html>";
    private const string SafeDocument = Prefix + "Safe control" + Suffix;
    private const string SeedDocument = Prefix + "<input value='" + Canary + "'>" + Suffix;
    private const string RecoveryDocument = Prefix + "<ul class='recovery-codes'><li><code>synthetic-recovery</code></li></ul>" + Suffix;
    private const string ExpectedFailure = "The fixed secret-redisplay failure must be observed.";
    private static readonly TimeSpan ImmediateFailureDeadline = TimeSpan.FromSeconds(1);

    /// <summary>Rejects incomplete documents, accepts released and already-ready documents, and immediately rejects either secret marker without page scripts.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DisabledPageScripts_When_HistoryDocumentBecomesReady_Then_SecretsFailImmediatelyAndOnlySafeDocumentsPass(string provider)
    {
        Assert.True(provider is PostgreSql or SqlServer);
        await using var session = await BrowserSession.CreateAsync(javaScript: false);
        var page = await session.Context.NewPageAsync();
        await HistoryDiagnosticExperiment.RunAsync(provider,
            (before, after) => HistoryReadinessProbe.RunAsync(page, Canary, before, after));
        foreach (var body in new[] { SeedDocument, RecoveryDocument })
        {
            await page.SetContentAsync(body);
            var exception = await Assert.ThrowsAsync<PlaywrightException>(() =>
                AdministratorHistory.RequireNoRedisplayAsync(page, Canary).WaitAsync(ImmediateFailureDeadline, TestContext.Current.CancellationToken));
            Assert.True(exception.Message.Contains(Failure, StringComparison.Ordinal), ExpectedFailure);
            // Removing material later cannot convert the observed failure to success.
            await page.SetContentAsync(SafeDocument);
            await AdministratorHistory.RequireNoRedisplayAsync(page, Canary);
        }
    }

    /// <summary>Uses a real no-store response, with no interception or cache override, to prove Firefox native refusal rather than accepting an inspector timeout.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RealNoStorePost_When_BackCannotRestoreDocument_Then_NativeRefusalHasNoResponseCommitOrResubmission(string provider)
    {
        Assert.True(provider is PostgreSql or SqlServer);
        await using var server = await HistoryControlServer.StartAsync();
        await using var session = await BrowserSession.CreateAsync(javaScript: false);
        var page = await session.Context.NewPageAsync();
        using var evidence = new HistoryNavigationEvidence(page, server);
        await page.GotoAsync(server.Origin);
        evidence.MoveTo(HistoryNavigationPhase.Submit);
        await page.Locator(HistoryControlServer.Submit).ClickAsync();
        await page.Locator(HistoryControlServer.Secret).WaitForAsync();
        evidence.MoveTo(HistoryNavigationPhase.Safe);
        await page.Locator(HistoryControlServer.Continue).ClickAsync();
        await page.WaitForURLAsync(server.Origin + HistoryControlServer.SafePath);
        await AdministratorHistory.RequireNoRedisplayAsync(page, HistoryControlServer.Canary);
        Assert.Equal(1, server.Posts);
        using var document = new HistoryDocumentEvidence(page, server);
        await document.RequirePriorAsync();
        evidence.MoveTo(HistoryNavigationPhase.Back);
        var outcome = await document.ObserveAsync(evidence);
        const string Firefox = "firefox";
        if (BrowserSession.Engine == Firefox)
        {
            Assert.Equal(HistoryOutcome.NativeRestoreRejected, outcome);
            Assert.Equal(1, server.Posts);
            evidence.MoveTo(HistoryNavigationPhase.Controls);
            // Use a separate page for seeded controls while Firefox settles the original history navigation.
            var controls = await session.Context.NewPageAsync();
            await RequireObserversAsync(controls, server);
            await RequireResponseObserverAsync(controls, server);
            Assert.Equal(1, server.Posts);
        }
        else
        {
            Assert.Equal(HistoryOutcome.DocumentObserved, outcome);
        }
        evidence.MoveTo(HistoryNavigationPhase.Complete);
    }

    /// <summary>Seeds a native-looking error only after real response/commit and commit-only events; the actual observer must reject both.</summary>
    private static async Task RequireObserversAsync(IPage page, HistoryControlServer server)
    {
        const string CacheMiss = "NS_ERROR_DOCUMENT_NOT_CACHED";
        const string Fragment = "#commit-control";
        var safe = server.Origin + HistoryControlServer.SafePath;
        await page.GotoAsync(safe);
        var response = await Assert.ThrowsAsync<PlaywrightException>(() => AdministratorHistory.ObserveAsync(page,
            HistoryControlServer.Canary, async () =>
            {
                await page.GotoAsync(safe);
                throw new PlaywrightException(CacheMiss);
            }));
        Assert.True(response.Message == CacheMiss);
        var commit = await Assert.ThrowsAsync<PlaywrightException>(() => AdministratorHistory.ObserveAsync(page,
            HistoryControlServer.Canary, async () =>
            {
                await page.GotoAsync(safe + Fragment);
                throw new PlaywrightException(CacheMiss);
            }));
        Assert.True(commit.Message == CacheMiss);
    }

    /// <summary>Independently proves the response listener using a real 204 navigation that receives headers but cannot commit a document.</summary>
    private static async Task RequireResponseObserverAsync(IPage page, HistoryControlServer server)
    {
        const int NoContentStatus = 204;
        const string Aborted = "NS_BINDING_ABORTED";
        const string CacheMiss = "NS_ERROR_DOCUMENT_NOT_CACHED";
        var responses = 0;
        var commits = 0;
        EventHandler<IResponse> response = (_, value) =>
        {
            if (value.Status == NoContentStatus && value.Request.IsNavigationRequest && value.Frame == page.MainFrame)
            {
                Interlocked.Increment(ref responses);
            }
        };
        EventHandler<IFrame> committed = (_, value) =>
        {
            if (value == page.MainFrame)
            {
                Interlocked.Increment(ref commits);
            }
        };
        page.Response += response;
        page.FrameNavigated += committed;
        try
        {
            var observed = await Assert.ThrowsAsync<PlaywrightException>(() => AdministratorHistory.ObserveAsync(page,
                HistoryControlServer.Canary, async () =>
                {
                    var aborted = await Assert.ThrowsAsync<PlaywrightException>(() => page.GotoAsync(server.Origin + HistoryControlServer.NoContentPath));
                    Assert.StartsWith(Aborted, aborted.Message, StringComparison.Ordinal);
                    throw new PlaywrightException(CacheMiss);
                }));
            Assert.True(observed.Message == CacheMiss);
            Assert.Equal(1, Volatile.Read(ref responses));
            Assert.Equal(0, Volatile.Read(ref commits));
        }
        finally
        {
            page.Response -= response;
            page.FrameNavigated -= committed;
        }
    }

}
