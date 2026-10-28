using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

/// <summary>Verifies Scalar's native same-document links retain keyboard navigation and visible focus.</summary>
internal static class ScalarNavigation
{
    private const string EndpointSelector = "ul.endpoints a.endpoint";
    private const string HrefAttribute = "href";
    private const string FragmentPattern = "^#[^\\s]+$";
    private const string TabKey = "Tab";
    private const string ActivateKey = "Enter";
    private const string OutlineWidth = "2px";
    private const string ReadOutline = "node => getComputedStyle(node).outlineWidth";
    private const int EndpointCount = 3;
    private static readonly (int Index, string Heading)[] Destinations = [(1, "Get a synthetic example"), (2, "Update a synthetic example")];

    /// <summary>Requires every overview anchor to remain a link with a nonempty fragment destination.</summary>
    internal static async Task RequireAsync(IPage page)
    {
        var endpoints = page.Locator(EndpointSelector);
        await Assertions.Expect(endpoints).ToHaveCountAsync(EndpointCount);
        for (var index = 0; index < EndpointCount; index++)
        {
            await Assertions.Expect(endpoints.Nth(index)).ToHaveRoleAsync(AriaRole.Link);
            await Assertions.Expect(endpoints.Nth(index)).ToHaveAttributeAsync(HrefAttribute, new Regex(FragmentPattern));
        }
        foreach (var (index, heading) in Destinations)
        {
            await endpoints.First.FocusAsync();
            for (var tab = 0; tab < index; tab++)
            {
                await page.Keyboard.PressAsync(TabKey);
            }
            await Assertions.Expect(endpoints.Nth(index)).ToBeFocusedAsync();
            Assert.Equal(OutlineWidth, await endpoints.Nth(index).EvaluateAsync<string>(ReadOutline));
            await page.Keyboard.PressAsync(ActivateKey);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new()
            {
                Name = heading,
                Exact = true
            })).ToBeInViewportAsync();
        }
    }
}
