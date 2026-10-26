using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks actual forbidden values and relevant encodings with private seeded detector controls.</summary>
internal static class BrowserSecretOracle
{
    private const string Canary = "synthetic+secret/with=encoding<&\"";
    private const string Surface = "surface-prefix:";
    private const string Storage = """
        async () => {
          const databases = indexedDB.databases ? await indexedDB.databases() : null;
          const cachesFound = 'caches' in window ? await caches.keys() : [];
          const workers = 'serviceWorker' in navigator ? await navigator.serviceWorker.getRegistrations() : [];
          return { local: Object.keys(localStorage), session: Object.keys(sessionStorage),
            databases: databases === null ? null : databases.length, caches: cachesFound.length, workers: workers.length,
            name: window.name, visible: JSON.stringify({local: {...localStorage}, session: {...sessionStorage}, cookie: document.cookie}),
            dom: document.documentElement.outerHTML };
        }
        """;
    private const string Theme = "identity-theme";

    /// <summary>Proves every encoding is detected before scanning actual private evidence; assertion failures never print the inspected strings.</summary>
    internal static void RequireAbsent(IEnumerable<string> observations, string[] secrets)
    {
        var probes = IndependentProbes;
        Assert.True(probes.All(value => Contains(Surface + value, [Canary])));
        Assert.False(Contains(Surface, [Canary]));
        Assert.True(secrets.Length > 0 && secrets.All(value => !string.IsNullOrEmpty(value)));
        var exposed = observations.Any(value => Contains(value, secrets));
        Assert.False(exposed);
    }

    /// <summary>Requires no application credential persistence on the currently loaded actual origin, including alternate browser storage facilities.</summary>
    internal static async Task StorageAsync(IPage page, string[] secrets)
    {
        var state = await page.EvaluateAsync<JsonElement>(Storage);
        var local = state.GetProperty(Local).EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.True(local.Length <= 1 && local.All(value => value == Theme));
        Assert.Equal(0, state.GetProperty(Session).GetArrayLength());
        Assert.Equal(0, state.GetProperty(Databases).GetInt32());
        Assert.Equal(0, state.GetProperty(Caches).GetInt32());
        Assert.Equal(0, state.GetProperty(Workers).GetInt32());
        Assert.True(string.IsNullOrEmpty(state.GetProperty(Name).GetString()));
        RequireAbsent([state.GetProperty(Visible).GetString()!, state.GetProperty(Dom).GetString()!], secrets);
    }

    /// <summary>Recognizes exact values or their relevant browser transport encodings without token-name heuristics.</summary>
    internal static bool Contains(string observation, string[] secrets) => secrets.SelectMany(Encodings)
        .Any(value => observation.Contains(value, StringComparison.Ordinal));

    /// <summary>Includes raw, URI/form, JSON and HTML representations while preserving finite actual-secret matching.</summary>
    private static string[] Encodings(string value) => [value, Uri.EscapeDataString(value), WebUtility.UrlEncode(value),
        JsonSerializer.Serialize(value)[1..^1], JsonSerializer.Serialize(value, BrowserJson)[1..^1], WebUtility.HtmlEncode(value)];
    private static readonly JsonSerializerOptions BrowserJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string[] IndependentProbes = [Canary, "synthetic%2Bsecret%2Fwith%3Dencoding%3C%26%22",
        "synthetic+secret/with=encoding<&\\\"", "synthetic+secret/with=encoding&lt;&amp;&quot;"];
    private const string Local = "local";
    private const string Session = "session";
    private const string Databases = "databases";
    private const string Caches = "caches";
    private const string Workers = "workers";
    private const string Name = "name";
    private const string Visible = "visible";
    private const string Dom = "dom";
}
