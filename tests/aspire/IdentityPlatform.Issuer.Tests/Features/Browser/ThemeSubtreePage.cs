using IdentityPlatform.AuthServer.Features.Accounts;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Only synthetic recovery-layout HTML and pinned embedded assets; no issuer, cookie, token, database or real credentials.</summary>
internal sealed class ThemeSubtreePage(bool owned)
{
    internal const string Origin = "https://theme-subtree.example.invalid";
    private readonly Dictionary<string, string> assets = [];
    private int posts;
    private int unexpectedRequests;
    internal int Posts => posts;
    internal int UnexpectedRequests => unexpectedRequests;

    /// <summary>Changes only exact selector ownership and font ownership in the candidate arm; source drift fails the setup.</summary>
    internal async Task AttachAsync(IPage page)
    {
        foreach (var name in new[] { "tokens.css", "login.css", "theme.js", "login.js" })
        {
            using var stream = typeof(AccountAssets).Assembly.GetManifestResourceStream("IdentityPlatform.AccountAssets." + name)
                ?? throw new InvalidOperationException("Shipped asset unavailable.");
            using var reader = new StreamReader(stream);
            assets["/assets/" + name] = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        }
        if (owned)
        {
            var css = assets["/assets/tokens.css"];
            css = Replace(css, ":root, body {", ":root, body, #theme-control, #theme {");
            css = Replace(css, ":root:not([data-theme=\"light\"]), :root:not([data-theme=\"light\"]) body {",
                ":root:not([data-theme=\"light\"]), :root:not([data-theme=\"light\"]) body, :root:not([data-theme=\"light\"]) #theme-control, :root:not([data-theme=\"light\"]) #theme {");
            css = Replace(css, ":root[data-theme=\"dark\"], :root[data-theme=\"dark\"] body {",
                ":root[data-theme=\"dark\"], :root[data-theme=\"dark\"] body, :root[data-theme=\"dark\"] #theme-control, :root[data-theme=\"dark\"] #theme {");
            assets["/assets/tokens.css"] = css;
            assets["/assets/login.css"] += "\n#theme-control, #theme { font-family: var(--font); }\n";
        }
        await page.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.GetLeftPart(UriPartial.Authority) == Origin && uri.Query.Length == 0 && uri.Fragment.Length == 0)
            {
                if (route.Request.Method == "GET" && assets.TryGetValue(uri.AbsolutePath, out var asset))
                {
                    await route.FulfillAsync(new()
                    {
                        ContentType = uri.AbsolutePath.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "text/javascript",
                        Body = asset
                    });
                    return;
                }
                if (uri.AbsolutePath.StartsWith("/journey/", StringComparison.Ordinal) &&
                    int.TryParse(uri.AbsolutePath[9..], out var step) && step is >= 0 and < 10 &&
                    route.Request.Method == (step == 0 ? "GET" : "POST"))
                {
                    if (step > 0)
                    {
                        Interlocked.Increment(ref posts);
                    }

                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/html; charset=utf-8",
                        Body = Html(step)
                    });
                    return;
                }
            }
            Interlocked.Increment(ref unexpectedRequests);
            await route.AbortAsync();
        });
    }

    /// <summary>Rejects a changed or ambiguous source selector instead of silently testing another transformation.</summary>
    private static string Replace(string source, string before, string after)
    {
        Assert.Equal(1, source.Split(before, StringSplitOptions.None).Length - 1);
        return source.Replace(before, after, StringComparison.Ordinal);
    }

    /// <summary>Mirrors the production shared layout and recovery-code form; repeated documents model navigation, not authentication behavior.</summary>
    private static string Html(int step) => $$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="light dark"><title>Enter a recovery code · IdentityPlatform</title>
        <script src="/assets/theme.js"></script><link rel="stylesheet" href="/assets/tokens.css"><link rel="stylesheet" href="/assets/login.css">
        <script src="/assets/login.js" defer></script></head><body>
        <a class="skip-link" href="#main">Skip to main content</a><header class="site-header">
        <span class="wordmark"><span class="brand-mark" aria-hidden="true">I</span> IdentityPlatform</span>
        <div class="header-tools"><span class="badge">Account access</span><label id="theme-control" hidden>Theme <select id="theme" aria-label="Theme"><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select></label></div>
        </header><main id="main" tabindex="-1"><section class="card" aria-labelledby="heading">
        <p class="eyebrow">Account security</p><h1 id="heading">Enter a recovery code</h1>
        <p>Use one of the single-use codes saved when you set up your authenticator. Continuing signs out your existing sessions and begins restricted authenticator replacement.</p>
        <form method="post" action="/journey/{{step + 1}}"><div class="field"><label for="recovery-code">Recovery code</label><input id="recovery-code" name="Code" type="text" autocomplete="one-time-code" minlength="32" maxlength="32" pattern="[0-9A-F]{32}" required spellcheck="false" autocapitalize="characters" aria-describedby="recovery-hint"></div>
        <p id="recovery-hint">Paste all 32 uppercase letters and numbers without spaces. This code will be used once.</p>
        <button class="primary" type="submit" id="continue">Begin authenticator recovery</button></form></section></main>
        <footer>Protected access, thoughtfully designed.</footer></body></html>
        """;
}
