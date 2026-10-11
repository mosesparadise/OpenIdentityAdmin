using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Accounts;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class BrowserStyleEvidenceTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Canvas_evidence_distinguishes_missing_body_rule_from_inline_override_and_healthy_theme(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        await BrowserEvidence.RunAsync(async () =>
        {
            var tokens = await ReadAssetAsync("tokens.css");
            var login = await ReadAssetAsync("login.css");
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.RouteAsync("https://style.example.invalid/**", route =>
            {
                var path = new Uri(route.Request.Url).AbsolutePath;
                return route.FulfillAsync(new()
                {
                    ContentType = path.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "text/html",
                    Body = path switch
                    {
                        "/assets/tokens.css" => tokens,
                        "/assets/login.css" => login,
                        _ => """
                            <!doctype html><html lang="en" data-theme="dark"><head><title>Style observation control</title>
                            <link rel="stylesheet" href="/assets/tokens.css"><link rel="stylesheet" href="/assets/login.css"></head>
                            <body><header class="site-header"><span class="wordmark">IdentityPlatform</span><span class="badge">Development</span></header>
                            <main><div class="card"><h1 id="heading">Style observation control</h1><p>Empty synthetic page</p></div></main>
                            <footer>Synthetic control</footer></body></html>
                            """
                    }
                });
            });
            await page.GotoAsync("https://style.example.invalid/");
            var healthy = await BrowserAccessibility.ReadStylesAsync(page);
            Assert.True(ContrastBoundaryTests.HasHealthyCanvas(healthy), "The actual healthy CSS must satisfy the boundary observation oracle.");
            RequireCanvas(healthy, "body-background-kind=opaque", "page-token=#020617", "text-token=#f8fafc",
                "body-scheme=dark", "root-scheme=dark", "theme=dark", "ready=complete", "body-connected=true", "single-body=true",
                "stylesheet=available", "sheet-disabled=false", "body-rule=true", "rule-background=true", "rule-color=true",
                "rule-base=true", "inline-override=false", "zero-margin=true", "font-applied=true", "line-height-applied=true", "min-height-applied=true");
            RequireStyle(healthy, "body: color=#f8fafc, fill=#f8fafc, background=#020617");

            await page.EvaluateAsync("""
                () => {
                    const sheet = document.querySelector('link[href="/assets/login.css"]').sheet;
                    const index = [...sheet.cssRules].findIndex(rule => rule.selectorText === 'body');
                    if (index < 0) throw new Error('Synthetic control requires the authored body rule');
                    sheet.deleteRule(index);
                }
                """);
            var absent = await BrowserAccessibility.ReadStylesAsync(page);
            Assert.False(ContrastBoundaryTests.HasHealthyCanvas(absent), "A missing body rule must remain a boundary finding.");
            RequireCanvas(absent, "body-background-kind=transparent", "page-token=#020617", "text-token=#f8fafc",
                "stylesheet=available", "body-rule=false", "rule-background=false", "rule-color=false", "rule-base=false",
                "inline-override=false", "zero-margin=false", "font-applied=false", "line-height-applied=false", "min-height-applied=false");
            RequireStyle(absent, ".wordmark: color=#f8fafc, fill=#f8fafc, background=unknown");

            await page.ReloadAsync();
            await page.Locator("body").EvaluateAsync("element => { element.style.background = '#ffffff'; element.style.color = '#000000'; }");
            var overridden = await BrowserAccessibility.ReadStylesAsync(page);
            Assert.False(ContrastBoundaryTests.HasHealthyCanvas(overridden), "White/black inline colors with unchanged dark tokens must remain a boundary finding.");
            RequireCanvas(overridden, "body-background-kind=opaque", "stylesheet=available", "body-rule=true", "rule-background=true",
                "rule-color=true", "rule-base=true", "inline-override=true", "zero-margin=true", "font-applied=true");
            RequireStyle(overridden, "body: color=#000000, fill=#000000, background=#ffffff");

            await page.Locator("link[href='/assets/login.css']").EvaluateAsync("element => element.remove()");
            RequireCanvas(await BrowserAccessibility.ReadStylesAsync(page), "stylesheet=absent", "body-rule=unknown");
        });
    }

    private static void RequireCanvas(JsonElement snapshot, params string[] expected)
    {
        var evidence = BrowserStyleEvidence.Describe(snapshot.GetProperty("canvas"));
        foreach (var value in expected)
        {
            Assert.True(evidence.Contains(value, StringComparison.Ordinal), "Controlled canvas observation mismatch: " + value + evidence);
        }
    }

    private static void RequireStyle(JsonElement snapshot, string expected) =>
        Assert.True(BrowserAccessibility.DescribeStyles(snapshot.GetProperty("styles")).Contains(expected, StringComparison.Ordinal),
            "Controlled style observation mismatch: " + expected + BrowserAccessibility.DescribeStyles(snapshot.GetProperty("styles")));

    private static async Task<string> ReadAssetAsync(string name)
    {
        using var stream = typeof(AccountAssets).Assembly.GetManifestResourceStream("IdentityPlatform.AccountAssets." + name)
            ?? throw new InvalidOperationException("The control requires the actual embedded account stylesheet.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
