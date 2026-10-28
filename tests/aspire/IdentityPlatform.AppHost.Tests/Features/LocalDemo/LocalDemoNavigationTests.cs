using System.Diagnostics;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Requires actual browser before/after and failure controls in each existing browser-provisioned LocalDemo provider lane.</summary>
public sealed class LocalDemoNavigationTests
{
    private const string ActionDeadline = "Timeout 15000ms exceeded";
    private const string NavigationDeadline = "Timeout 20000ms exceeded";
    private const string AbandonedNavigation = "The pending navigation waiter was abandoned after the action failed.";
    private const int DelayedActionMilliseconds = 7000;
    private const string DelayAction = "(button, delay) => { button.disabled = true; setTimeout(() => button.disabled = false, delay); }";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private static readonly TimeSpan JoinedNavigationLowerBound = TimeSpan.FromSeconds(19);

    /// <summary>The original click aborts a permitted navigation; the shared demo helper completes the same chain with one POST in a fresh context.</summary>
    [Theory]
    [InlineData(PostgreSql, Traits = ["Provider", PostgreSql])]
    [InlineData(SqlServer, Traits = ["Provider", SqlServer])]
    public async Task Given_RedirectChainBeyondActionBudget_When_SubmittedWithOwnedNavigation_Then_ExactDestinationCompletesWithoutResubmission(string provider)
    {
        Assert.Contains(provider, new[] { PostgreSql, SqlServer });
        await using var fixture = await LocalDemoNavigationFixture.CreateAsync();
        await using (var original = await fixture.ContextAsync())
        {
            var page = await original.NewPageAsync();
            await fixture.OpenAsync(page, LocalDemoNavigationFixture.FormPath);
            var error = await Assert.ThrowsAsync<TimeoutException>(() => page.Locator(LocalDemoNavigationFixture.SubmitSelector).ClickAsync());
            Assert.Contains(ActionDeadline, error.Message, StringComparison.Ordinal);
            Assert.Equal(1, fixture.Submissions);
        }
        await using var corrected = await fixture.ContextAsync();
        var current = await corrected.NewPageAsync();
        await fixture.OpenAsync(current, LocalDemoNavigationFixture.FormPath);
        await LocalDemoNavigation.SubmitAsync(current, current.Locator(LocalDemoNavigationFixture.SubmitSelector), fixture.Destination);
        Assert.Equal(fixture.Destination, current.Url);
        Assert.True(await current.Locator(LocalDemoNavigationFixture.CompleteSelector).IsVisibleAsync());
        Assert.Equal(2, fixture.Submissions);
    }

    /// <summary>Retains actionability, caps unfinished navigation and proves delayed action cannot restart the already-armed navigation budget.</summary>
    [Theory]
    [InlineData(PostgreSql, Traits = ["Provider", PostgreSql])]
    [InlineData(SqlServer, Traits = ["Provider", SqlServer])]
    public async Task Given_UnavailableActionOrUnfinishedNavigation_When_SubmittedOnce_Then_OriginalDeadlinesDenyAndBothWaitersAreObserved(string provider)
    {
        Assert.Contains(provider, new[] { PostgreSql, SqlServer });
        await using var fixture = await LocalDemoNavigationFixture.CreateAsync();
        await RequireUnavailableActionAsync(fixture);
        await using (var hanging = await fixture.ContextAsync())
        {
            var current = await hanging.NewPageAsync();
            await fixture.OpenAsync(current, LocalDemoNavigationFixture.HangingPath);
            await RequireNavigationTimeoutAsync(fixture, current);
            Assert.Equal(1, fixture.Submissions);
        }
        await using var delayed = await fixture.ContextAsync();
        var action = await delayed.NewPageAsync();
        await fixture.OpenAsync(action, LocalDemoNavigationFixture.FormPath);
        await action.Locator(LocalDemoNavigationFixture.SubmitSelector).EvaluateAsync(DelayAction, DelayedActionMilliseconds);
        await RequireNavigationTimeoutAsync(fixture, action);
        Assert.Equal(2, fixture.Submissions);
    }

    /// <summary>Proves action denial sends nothing and its primary error survives joining the outstanding navigation waiter.</summary>
    private static async Task RequireUnavailableActionAsync(LocalDemoNavigationFixture fixture)
    {
        await using var disabled = await fixture.ContextAsync();
        var page = await disabled.NewPageAsync();
        await fixture.OpenAsync(page, LocalDemoNavigationFixture.DisabledPath);
        var elapsed = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<TimeoutException>(() => LocalDemoNavigation.SubmitAsync(page,
            page.Locator(LocalDemoNavigationFixture.SubmitSelector), fixture.Destination));
        Assert.Contains(ActionDeadline, error.Message, StringComparison.Ordinal);
        Assert.True(elapsed.Elapsed >= JoinedNavigationLowerBound, AbandonedNavigation);
        Assert.Equal(0, fixture.Submissions);
    }

    /// <summary>Requires the original twenty-second native navigation failure instead of accepting click completion or resetting the wait.</summary>
    private static async Task RequireNavigationTimeoutAsync(LocalDemoNavigationFixture fixture, Microsoft.Playwright.IPage page)
    {
        var error = await Assert.ThrowsAsync<TimeoutException>(() => LocalDemoNavigation.SubmitAsync(page,
            page.Locator(LocalDemoNavigationFixture.SubmitSelector), fixture.Destination));
        Assert.Contains(NavigationDeadline, error.Message, StringComparison.Ordinal);
    }
}
