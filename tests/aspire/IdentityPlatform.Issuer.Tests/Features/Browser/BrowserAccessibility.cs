using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal static class BrowserAccessibility
{
    private static readonly SearchValues<char> HexCharacters = SearchValues.Create("0123456789abcdefABCDEF");
    private static readonly string[] Targets =
    [
        "#submit", "#theme", "#show-password", "#username", "#password", "#heading", "#error-summary > p",
        ".error-summary > p", ".wordmark", ".brand-mark", ".badge", ".eyebrow", ".subtitle", ".footnote",
        "footer", "label", "strong", "p", ".skip-link"
    ];

    public static async Task RequireNoViolationsAsync(IPage page, Func<Task>? captureFailure = null)
    {
        var result = await page.RunAxe();
        if (result.Violations.Length > 0)
        {
            var message = Describe(result);
            if (captureFailure is not null)
            {
                try
                {
                    await captureFailure();
                }
                catch (Exception exception) when (exception is PlaywrightException or TimeoutException or Xunit.Sdk.XunitException or IOException)
                {
                    message += "; diagnostic capture failed";
                }
            }
            Assert.Fail(message);
        }
    }

    internal static string Describe(AxeResult result)
    {
        // Respect the library's Newtonsoft converters, including its selector and JObject data shapes.
        using var json = JsonDocument.Parse(JsonConvert.SerializeObject(result, AxeJsonSerializerSettings.Default));
        return DescribeJson(json.RootElement);
    }

    private static string DescribeJson(JsonElement result)
    {
        // Never retain axe HTML, URLs, text, selectors outside this fixed set, or arbitrary check data.
        var details = new List<string>();
        foreach (var violation in result.GetProperty("violations").EnumerateArray())
        {
            var rule = violation.GetProperty("id").GetString() == "color-contrast" ? "color-contrast" : "other accessibility rule";
            foreach (var node in violation.GetProperty("nodes").EnumerateArray())
            {
                var target = node.GetProperty("target").EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => Targets.FirstOrDefault(known => known == value.GetString()) ?? "other element");
                var checks = new List<string>();
                foreach (var check in node.GetProperty("any").EnumerateArray())
                {
                    if (check.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        checks.Add($"foreground={Color(data, "fgColor")}, background={Color(data, "bgColor")}, contrast={Ratio(data)}");
                    }
                }
                details.Add($"{rule}: {string.Join("/", target)} ({string.Join("; ", checks)})");
            }
        }
        return "Automated accessibility violations: " + string.Join("; ", details);
    }

    private static string Color(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return "unknown";
        }
        var color = value.GetString();
        return color is { Length: 7 } && color[0] == '#' && color.AsSpan(1).IndexOfAnyExcept(HexCharacters) < 0
            ? color.ToLowerInvariant() : "unknown";
    }

    private static string Ratio(JsonElement data) =>
        data.TryGetProperty("contrastRatio", out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var ratio) && double.IsFinite(ratio) && ratio is >= 0 and <= 21
            ? ratio.ToString("0.###", CultureInfo.InvariantCulture) : "unknown";
}
