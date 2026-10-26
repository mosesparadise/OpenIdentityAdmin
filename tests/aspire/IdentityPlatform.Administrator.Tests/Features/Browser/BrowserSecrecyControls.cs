using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Seeds isolated real browser surfaces to prove observation and matching before crediting actual credential absence.</summary>
internal static class BrowserSecrecyControls
{
    private const string Network = "synthetic-network-probe+with/encoding";
    private const string Body = "synthetic-body-probe";
    private const string Console = "synthetic-console-probe";
    private const string ConsoleObject = "synthetic-console-object-probe";
    private const string Storage = "synthetic-storage-probe";
    private const string Dom = "synthetic-dom-probe";
    private const string Document = "<p>synthetic-body-probe</p>";
    private const string Query = "?probe=";
    private const string Seed = "values => { console.log(values.console); console.log({token:values.object}); localStorage.setItem('probe',values.storage); sessionStorage.setItem('probe',values.storage); window.name=values.storage; document.body.append(document.createTextNode(values.dom)); return JSON.stringify({local:{...localStorage},session:{...sessionStorage},name:window.name,dom:document.documentElement.outerHTML}); }";
    private static readonly string[] NetworkProbes = [Network, Body, Console, ConsoleObject];

    /// <summary>Uses a separate browser and a fixed test-owned TLS document, with no authentication, credentials or persistent output.</summary>
    internal static async Task RunAsync(BrowserHttpsFixture fixture)
    {
        BrowserTrafficRequests.RequireControls();
        await using var session = await BrowserSession.CreateAsync();
        await BrowserTrafficLifetime.RunAsync(session.Context, async traffic =>
        {
            var page = await session.Context.NewPageAsync();
            var target = fixture.StageAttacker(fixture.Profile.Issuer, Document);
            await page.GotoAsync(target.AbsoluteUri + Query + Uri.EscapeDataString(Network));
            var observed = await page.EvaluateAsync<string>(Seed, new
            {
                console = Console,
                @object = ConsoleObject,
                storage = Storage,
                dom = Dom
            });
            Assert.True(BrowserSecretOracle.Contains(observed, [Storage]) && BrowserSecretOracle.Contains(observed, [Dom]));
            Assert.Throws<Xunit.Sdk.FalseException>(() => BrowserSecretOracle.RequireAbsent([observed], [Storage]));
            await traffic.RequireProbesAsync(NetworkProbes);
        });
    }
}
