using Microsoft.Playwright;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

/// <summary>Retains the existing Scalar model-expansion checks independently of endpoint navigation semantics.</summary>
internal static class ScalarModels
{
    private const string Sidebar = ".t-doc__sidebar";
    private const string Models = "Models";
    private const string More = "Show more";
    private const string Problem = "ProblemDetails";
    private const string Status = "status";
    private const string Collapsed = "button[aria-expanded=false]";

    /// <summary>Requires the problem schema to remain readable after expanding the model catalogue and properties.</summary>
    internal static async Task RequireAsync(IPage page)
    {
        await page.Locator(Sidebar).GetByText(Models, new()
        {
            Exact = true
        }).ClickAsync();
        var models = page.GetByRole(AriaRole.Region, new()
        {
            Name = Models,
            Exact = true
        });
        await models.ScrollIntoViewIfNeededAsync();
        var more = models.GetByRole(AriaRole.Button, new()
        {
            Name = More,
            Exact = true
        });
        if (await more.IsVisibleAsync())
        {
            await more.ClickAsync();
        }
        await page.Locator(Sidebar).GetByText(Problem, new()
        {
            Exact = true
        }).ClickAsync();
        var problem = page.GetByRole(AriaRole.Region, new()
        {
            Name = Problem,
            Exact = true
        });
        await problem.ScrollIntoViewIfNeededAsync();
        var expand = problem.Locator(Collapsed).First;
        if (await expand.IsVisibleAsync())
        {
            await expand.ClickAsync();
        }
        await Assertions.Expect(problem.GetByText(Status, new()
        {
            Exact = true
        }).First).ToBeVisibleAsync();
    }
}
