using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Owns one form action and one exact-destination navigation under their distinct, nonrenewing browser budgets.</summary>
internal static class LocalDemoNavigation
{
    internal const int ActionTimeoutMilliseconds = 15000;
    internal const int NavigationTimeoutMilliseconds = 20000;

    /// <summary>Preserves the demo's action and navigation defaults for all other browser operations.</summary>
    internal static void Configure(IBrowserContext context)
    {
        context.SetDefaultTimeout(ActionTimeoutMilliseconds);
        context.SetDefaultNavigationTimeout(NavigationTimeoutMilliseconds);
    }

    /// <summary>Arms the existing navigation budget before one click; observes both tasks and retains an action failure as primary.</summary>
    internal static async Task SubmitAsync(IPage page, ILocator submit, string destination)
    {
        var navigation = page.WaitForURLAsync(value => string.Equals(value, destination, StringComparison.Ordinal),
            new()
            {
                Timeout = NavigationTimeoutMilliseconds
            });
        ExceptionDispatchInfo? actionFailure = null;
        try
        {
            var action = new LocatorClickOptions { Timeout = ActionTimeoutMilliseconds };
#pragma warning disable CS0612 // Pinned Playwright 1.63 still defaults false; its deprecation announces a future true default. Navigation is explicitly awaited above.
            action.NoWaitAfter = true;
#pragma warning restore CS0612
            await submit.ClickAsync(action);
        }
        catch (Exception error)
        {
            actionFailure = ExceptionDispatchInfo.Capture(error);
        }
        try
        {
            await navigation;
        }
        catch when (actionFailure is not null) { }
        actionFailure?.Throw();
    }
}
