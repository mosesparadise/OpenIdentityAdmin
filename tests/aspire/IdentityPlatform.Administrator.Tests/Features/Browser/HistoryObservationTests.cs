using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves history observations wait for readiness without waiting away exposed secret material.</summary>
[Trait("Category", "Browser")]
public sealed class HistoryObservationTests
{
    private const string Origin = "https://history.example.invalid";
    private const string Pending = "/pending";
    private const string Canary = "synthetic-history-canary";
    private const string Failure = "Committed account secrets reappeared during browser history traversal.";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string Prefix = "<!doctype html><html><body>";
    private const string Suffix = "</body></html>";
    private const string SafeDocument = Prefix + "Safe control" + Suffix;
    private const string HeldDocument = Prefix + "<img src='/pending' alt=''>Safe control" + Suffix;
    private const string SeedDocument = Prefix + "<input value='" + Canary + "'>" + Suffix;
    private const string RecoveryDocument = Prefix + "<ul class='recovery-codes'><li><code>synthetic-recovery</code></li></ul>" + Suffix;
    private const string ExpectedFailure = "The fixed secret-redisplay failure must be observed.";
    private const string RoutePattern = Origin + "/**";
    private const string Readiness = "document.readyState";
    private const string Complete = "complete";
    private const string SvgContentType = "image/svg+xml";
    private const string HtmlContentType = "text/html";
    private const string Image = "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/>";
    private const int IncompleteTimeoutMilliseconds = 250;
    private static readonly TimeSpan ImmediateFailureDeadline = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ResourceDeadline = TimeSpan.FromSeconds(5);

    /// <summary>Rejects incomplete documents, accepts released and already-ready documents, and immediately rejects either secret marker without page scripts.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DisabledPageScripts_When_HistoryDocumentBecomesReady_Then_SecretsFailImmediatelyAndOnlySafeDocumentsPass(string provider)
    {
        Assert.True(provider is PostgreSql or SqlServer);
        await using var session = await BrowserSession.CreateAsync(javaScript: false);
        var page = await session.Context.NewPageAsync();
        await RequireReadinessAsync(page);
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

    /// <summary>Holds a real subresource so the predicate must reject loading and then observe completion using driver polling.</summary>
    private static async Task RequireReadinessAsync(IPage page)
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync(RoutePattern, route => RespondAsync(route, requested, release));
        try
        {
            await page.GotoAsync(Origin, new()
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });
            await requested.Task.WaitAsync(ResourceDeadline, TestContext.Current.CancellationToken);
            Assert.True(await page.EvaluateAsync<string>(Readiness) != Complete);
            await Assert.ThrowsAsync<TimeoutException>(() => AdministratorHistory.RequireNoRedisplayAsync(page, Canary, IncompleteTimeoutMilliseconds));
            release.TrySetResult();
            await AdministratorHistory.RequireNoRedisplayAsync(page, Canary);
            await AdministratorHistory.RequireNoRedisplayAsync(page, Canary);
        }
        finally
        {
            release.TrySetResult();
            await page.UnrouteAllAsync(new()
            {
                Behavior = UnrouteBehavior.Wait
            });
        }
    }

    /// <summary>Serves only synthetic content and releases the held image through an explicit test-owned signal.</summary>
    private static async Task RespondAsync(IRoute route, TaskCompletionSource requested, TaskCompletionSource release)
    {
        if (new Uri(route.Request.Url).AbsolutePath == Pending)
        {
            requested.TrySetResult();
            await release.Task;
            await route.FulfillAsync(new()
            {
                ContentType = SvgContentType,
                Body = Image
            });
            return;
        }
        await route.FulfillAsync(new()
        {
            ContentType = HtmlContentType,
            Body = HeldDocument
        });
    }
}
