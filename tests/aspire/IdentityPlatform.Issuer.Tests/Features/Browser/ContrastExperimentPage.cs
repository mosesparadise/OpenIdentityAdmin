using IdentityPlatform.AuthServer.Features.Accounts;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal sealed class ContrastExperimentPage
{
    private readonly Dictionary<string, string> assets = [];
    private int posts;
    private int unexpectedRequests;

    internal int Posts => posts;
    internal int UnexpectedRequests => unexpectedRequests;

    internal async Task AttachAsync(IPage page, bool ownedTokens = false)
    {
        foreach (var name in new[] { "tokens.css", "login.css", "theme.js", "login.js" })
        {
            using var stream = typeof(AccountAssets).Assembly.GetManifestResourceStream("IdentityPlatform.AccountAssets." + name)
                ?? throw new InvalidOperationException("The experiment requires the shipped account assets.");
            using var reader = new StreamReader(stream);
            assets["/assets/" + name] = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        }
        if (ownedTokens)
        {
            assets["/assets/tokens.css"] = OwnTokens(assets["/assets/tokens.css"]);
        }
        await page.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.GetLeftPart(UriPartial.Authority) != "https://contrast.example.invalid" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            {
                Interlocked.Increment(ref unexpectedRequests);
                await route.AbortAsync();
            }
            else if (route.Request.Method == "GET" && assets.TryGetValue(uri.AbsolutePath, out var asset))
            {
                await route.FulfillAsync(new()
                {
                    ContentType = uri.AbsolutePath.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8",
                    Body = asset
                });
            }
            else if (uri.AbsolutePath == "/account/login" && route.Request.Method is "GET" or "POST")
            {
                var error = route.Request.Method == "POST";
                if (error)
                {
                    Interlocked.Increment(ref posts);
                }
                await route.FulfillAsync(new()
                {
                    ContentType = "text/html; charset=utf-8",
                    Body = Html(error)
                });
            }
            else
            {
                Interlocked.Increment(ref unexpectedRequests);
                await route.AbortAsync();
            }
        });
    }

    internal static string OwnTokens(string css)
    {
        // Diagnostic asset only; the stylesheet declarations and scripts are unchanged.
        foreach (var (before, after) in new[]
        {
            (":root {", ":root, body {"),
            (":root:not([data-theme=\"light\"]) {", ":root:not([data-theme=\"light\"]), :root:not([data-theme=\"light\"]) body {"),
            (":root[data-theme=\"dark\"] {", ":root[data-theme=\"dark\"], :root[data-theme=\"dark\"] body {")
        })
        {
            Assert.True(css.Split(before, StringSplitOptions.None).Length == 2, "Token ownership experiment requires each reviewed selector exactly once.");
            css = css.Replace(before, after, StringComparison.Ordinal);
        }
        return css;
    }

    // Fixed synthetic layout: no issuer/cookie/token/antiforgery data. Asset bytes and script order are shipped unchanged.
    private static string Html(bool error)
    {
        var summary = error ? "<div id='error-summary' class='error-summary' tabindex='-1'><p>Unable to sign in. Check your details or start a new sign-in request.</p></div>" : "";
        var description = error ? " aria-describedby='error-summary'" : "";
        var username = error ? "synthetic.unknown.user" : "";
        return $$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="light dark"><title>Sign in · IdentityPlatform</title>
        <script src="/assets/theme.js"></script><link rel="stylesheet" href="/assets/tokens.css"><link rel="stylesheet" href="/assets/login.css">
        <script src="/assets/login.js" defer></script></head><body>
        <a class="skip-link" href="#main">Skip to main content</a><header class="site-header">
        <span class="wordmark"><span class="brand-mark" aria-hidden="true">I</span> IdentityPlatform</span>
        <div class="header-tools"><span class="badge">Development</span><label id="theme-control" hidden>Theme
        <select id="theme" aria-label="Theme"><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select>
        </label></div></header><main id="main" tabindex="-1"><section class="card" aria-labelledby="heading">
        <div class="card-icon" aria-hidden="true">↗</div><p class="eyebrow">Welcome back</p><h1 id="heading">Sign in</h1>
        <p class="subtitle">Continue to <strong>IdentityPlatform pilot</strong></p>
        {{summary}}
        <form method="post" id="login-form"><div class="field"><label for="username">Username</label>
        <input id="username" name="Username" type="text" required maxlength="128" autocomplete="username" autocapitalize="none" spellcheck="false" value="{{username}}"{{description}}>
        </div><div class="field"><label for="password">Password</label><div class="password-field">
        <input id="password" name="Password" type="password" required maxlength="256" autocomplete="current-password"{{description}}>
        <button id="show-password" type="button" hidden>Show password</button></div></div>
        <button type="submit" class="primary" id="submit">Sign in <span aria-hidden="true">→</span></button></form>
        <p class="footnote">Your credentials stay with IdentityPlatform.</p></section></main><footer>Protected access, thoughtfully designed.</footer>
        </body></html>
        """;
    }
}
