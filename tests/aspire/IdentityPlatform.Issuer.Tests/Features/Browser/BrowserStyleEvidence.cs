using System.Buffers;
using System.Text.Json;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal static class BrowserStyleEvidence
{
    private static readonly SearchValues<char> HexCharacters = SearchValues.Create("0123456789abcdefABCDEF");

    internal static string Describe(JsonElement state)
    {
        // Treat browser-produced data as untrusted. Only fixed categories, flags and hex colors survive.
        if (state.ValueKind != JsonValueKind.Object)
        {
            return "; canvas evidence unavailable";
        }
        return "; canvas evidence: " + string.Join(", ", new[]
        {
            "body-background-kind=" + Category(state, "bodyBackgroundKind", "opaque", "transparent", "partial", "unsupported"),
            "root-background-kind=" + Category(state, "rootBackgroundKind", "opaque", "transparent", "partial", "unsupported"),
            "page-token=" + Color(state, "pageToken"), "text-token=" + Color(state, "textToken"),
            "root-page-token=" + Color(state, "rootPageToken"), "root-text-token=" + Color(state, "rootTextToken"),
            "body-color=" + Color(state, "bodyColor"), "body-background=" + Color(state, "bodyBackground"),
            "body-scheme=" + Category(state, "bodyScheme", "light", "dark", "light dark", "normal"),
            "root-scheme=" + Category(state, "rootScheme", "light", "dark", "light dark", "normal"),
            "theme-page-token=" + Color(state, "themePageToken"), "theme-control-page-token=" + Color(state, "themeControlPageToken"),
            "theme-scheme=" + Category(state, "themeScheme", "light", "dark", "light dark", "normal"),
            "theme-control-scheme=" + Category(state, "themeControlScheme", "light", "dark", "light dark", "normal"),
            "theme-font-applied=" + Flag(state, "themeFontApplied"), "theme-control-font-applied=" + Flag(state, "themeControlFontApplied"),
            "theme=" + Category(state, "theme", "light", "dark", "system"),
            "ready=" + Category(state, "ready", "loading", "interactive", "complete"),
            "visibility=" + Category(state, "visibility", "visible", "hidden"),
            "focus=" + Flag(state, "focus"), "body-connected=" + Flag(state, "bodyConnected"),
            "single-body=" + Flag(state, "singleBody"),
            "stylesheet=" + Category(state, "stylesheet", "absent", "available", "unavailable"),
            "sheet-disabled=" + Flag(state, "sheetDisabled"), "body-rule=" + Flag(state, "bodyRule"),
            "rule-background=" + Flag(state, "ruleBackground"), "rule-color=" + Flag(state, "ruleColor"),
            "rule-base=" + Flag(state, "ruleBase"), "inline-override=" + Flag(state, "inlineOverride"),
            "zero-margin=" + Flag(state, "zeroMargin"), "font-applied=" + Flag(state, "fontApplied"),
            "line-height-applied=" + Flag(state, "lineHeightApplied"), "min-height-applied=" + Flag(state, "minHeightApplied")
        });
    }

    private static string Category(JsonElement state, string key, params string[] allowed) =>
        state.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String &&
        allowed.Contains(value.GetString(), StringComparer.Ordinal) ? value.GetString()! : "unknown";

    private static string Flag(JsonElement state, string key) => state.TryGetProperty(key, out var value) ? value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => "unknown"
    } : "unknown";

    private static string Color(JsonElement state, string key)
    {
        var color = state.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return color is { Length: 7 } && color[0] == '#' && color.AsSpan(1).IndexOfAnyExcept(HexCharacters) < 0
            ? color.ToLowerInvariant() : "unknown";
    }

    // Invoked only after a failed scan (or by the synthetic observation control). No DOM/style mutation.
    // Capture all colors first: geometry and CSSOM reads can themselves influence style resolution.
    internal const string Snapshot = """
        selectors => {
            const color = value => {
                const match = /^rgba?\((\d+), (\d+), (\d+)(?:, ([\d.]+))?\)$/.exec(value);
                if (!match || match.slice(1, 4).some(channel => Number(channel) > 255)) return {hex: 'unknown', kind: 'unsupported'};
                const alpha = match[4] === undefined ? 1 : Number(match[4]);
                if (!Number.isFinite(alpha) || alpha < 0 || alpha > 1) return {hex: 'unknown', kind: 'unsupported'};
                return {hex: alpha === 1 ? '#' + match.slice(1, 4).map(channel => Number(channel).toString(16).padStart(2, '0')).join('') : 'unknown',
                    kind: alpha === 1 ? 'opaque' : alpha === 0 ? 'transparent' : 'partial'};
            };
            const elements = selectors.map(selector => document.querySelector(selector));
            const styles = elements.flatMap((element, index) => {
                if (!element) return [];
                const style = getComputedStyle(element);
                return [{index, color: color(style.color).hex, fill: color(style.webkitTextFillColor).hex,
                    background: color(style.backgroundColor).hex}];
            });
            const body = document.body, root = document.documentElement;
            const bodyStyle = getComputedStyle(body), rootStyle = getComputedStyle(root);
            const theme = document.getElementById('theme'), themeControl = document.getElementById('theme-control');
            const themeStyle = theme ? getComputedStyle(theme) : null, themeControlStyle = themeControl ? getComputedStyle(themeControl) : null;
            const canvas = {
                bodyBackgroundKind: color(bodyStyle.backgroundColor).kind, rootBackgroundKind: color(rootStyle.backgroundColor).kind,
                pageToken: bodyStyle.getPropertyValue('--page').trim(), textToken: bodyStyle.getPropertyValue('--text').trim(),
                rootPageToken: rootStyle.getPropertyValue('--page').trim(), rootTextToken: rootStyle.getPropertyValue('--text').trim(),
                bodyColor: color(bodyStyle.color).hex, bodyBackground: color(bodyStyle.backgroundColor).hex,
                bodyScheme: bodyStyle.colorScheme, rootScheme: rootStyle.colorScheme,
                themePageToken: themeStyle?.getPropertyValue('--page').trim(), themeControlPageToken: themeControlStyle?.getPropertyValue('--page').trim(),
                themeScheme: themeStyle?.colorScheme, themeControlScheme: themeControlStyle?.colorScheme,
                themeFontApplied: themeStyle?.fontFamily.split(',')[0].trim() === 'Inter',
                themeControlFontApplied: themeControlStyle?.fontFamily.split(',')[0].trim() === 'Inter',
                theme: root.getAttribute('data-theme') ?? 'system', ready: document.readyState,
                visibility: document.visibilityState, focus: document.hasFocus(),
                bodyConnected: body.isConnected && body.ownerDocument === document && root.contains(body),
                singleBody: document.querySelectorAll('body').length === 1,
                inlineOverride: ['all', 'background', 'background-color', 'color', 'font', 'font-family', 'margin', 'line-height']
                    .some(property => body.style.getPropertyValue(property) !== ''),
                zeroMargin: ['marginTop', 'marginRight', 'marginBottom', 'marginLeft'].every(property => bodyStyle[property] === '0px'),
                fontApplied: bodyStyle.fontFamily.split(',')[0].trim() === 'Inter',
                lineHeightApplied: Math.abs(parseFloat(bodyStyle.lineHeight) / parseFloat(bodyStyle.fontSize) - 1.6) < 0.001,
                minHeightApplied: bodyStyle.minHeight === innerHeight + 'px'
            };
            for (const node of styles) {
                const element = elements[node.index], rect = element.getBoundingClientRect();
                Object.assign(node, {focus: element === document.activeElement, hover: element.matches(':hover'),
                    top: Math.round(rect.top), height: Math.round(rect.height), scroll: Math.round(scrollY)});
            }
            const link = [...document.querySelectorAll('link[rel="stylesheet"]')].find(element => {
                const href = new URL(element.href);
                return href.origin === location.origin && href.pathname === '/assets/login.css' && !href.search && !href.hash;
            });
            canvas.stylesheet = link ? 'unavailable' : 'absent';
            if (link?.sheet) {
                canvas.sheetDisabled = link.sheet.disabled;
                try {
                    const rule = [...link.sheet.cssRules].find(rule => rule.selectorText === 'body');
                    canvas.stylesheet = 'available';
                    canvas.bodyRule = !!rule;
                    canvas.ruleBackground = !!rule && rule.style.getPropertyValue('background') === 'var(--page)';
                    canvas.ruleColor = !!rule && rule.style.getPropertyValue('color') === 'var(--text)';
                    canvas.ruleBase = !!rule && rule.style.getPropertyValue('font-family') === 'var(--font)' &&
                        rule.style.getPropertyValue('margin') === '0px' && rule.style.getPropertyValue('line-height') === '1.6' &&
                        rule.style.getPropertyValue('min-height') === '100svh';
                } catch { /* Inaccessible CSSOM is not evidence that a rule is absent. */ }
            }
            return {styles, canvas};
        }
        """;
}
