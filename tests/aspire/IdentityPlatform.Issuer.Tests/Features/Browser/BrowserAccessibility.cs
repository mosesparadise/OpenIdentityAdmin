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
        "footer", "label", "strong", "p", ".skip-link", "body", "html", ".card", "#contrast-probe"
    ];

    public static async Task RequireNoViolationsAsync(IPage page, Func<Task>? captureFailure = null)
    {
        var result = await page.RunAxe();
        if (result.Violations.Length > 0)
        {
            var message = Describe(result);
            try
            {
                var snapshot = await ReadStylesAsync(page);
                message += DescribeSnapshot(snapshot);
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException or InvalidOperationException)
            {
                message += "; style snapshot unavailable";
            }
            if (captureFailure is not null)
            {
                try
                {
                    await captureFailure();
                    var later = await ReadStylesAsync(page);
                    message += "; after diagnostic capture (later state)" + DescribeSnapshot(later);
                }
                catch (Exception exception) when (exception is PlaywrightException or TimeoutException or Xunit.Sdk.XunitException or IOException or InvalidOperationException)
                {
                    message += "; diagnostic capture failed";
                }
            }
            Assert.Fail(message);
        }
    }

    internal static Task<JsonElement> ReadStylesAsync(IPage page) => page.EvaluateAsync<JsonElement>(BrowserStyleEvidence.Snapshot, Targets);

    internal static string DescribeSnapshot(JsonElement snapshot) =>
        snapshot.ValueKind == JsonValueKind.Object && snapshot.TryGetProperty("styles", out var styles) && styles.ValueKind == JsonValueKind.Array &&
        snapshot.TryGetProperty("canvas", out var canvas) ? DescribeStyles(styles) + BrowserStyleEvidence.Describe(canvas) : "; style snapshot unavailable";

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
                var target = DescribeTarget(node.GetProperty("target"));
                var checks = new List<string>();
                foreach (var check in node.GetProperty("any").EnumerateArray())
                {
                    if (check.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        checks.Add($"foreground={Color(data, "fgColor")}, background={Color(data, "bgColor")}, contrast={Ratio(data)}");
                    }
                }
                details.Add($"{rule}: {target} ({string.Join("; ", checks)})");
            }
        }
        return "Automated accessibility violations: " + string.Join("; ", details);
    }

    // AxeSelector serializes a single selector as a string and frame/shadow chains as arrays.
    private static string DescribeTarget(JsonElement target) => target.ValueKind switch
    {
        JsonValueKind.String => Targets.FirstOrDefault(known => known == target.GetString()) ?? "other element",
        JsonValueKind.Array => string.Join("/", target.EnumerateArray().Select(DescribeTarget)),
        _ => "other element"
    };

    internal static string DescribeStyles(JsonElement styles)
    {
        var details = new List<string>();
        foreach (var node in styles.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("index", out var indexValue) || indexValue.ValueKind != JsonValueKind.Number || !indexValue.TryGetInt32(out var index) || index < 0 || index >= Targets.Length)
            {
                continue;
            }
            details.Add($"{Targets[index]}: color={Color(node, "color")}, fill={Color(node, "fill")}, background={Color(node, "background")}, " +
                $"focus={Flag(node, "focus")}, hover={Flag(node, "hover")}, top={Coordinate(node, "top")}, height={Coordinate(node, "height")}, scroll={Coordinate(node, "scroll")}");
        }
        return "; computed styles: " + string.Join("; ", details);
    }

    private static bool Flag(JsonElement node, string key) => node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static string Coordinate(JsonElement node, string key) => node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number) && number is >= -100000 and <= 100000 ? number.ToString(CultureInfo.InvariantCulture) : "unknown";

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
