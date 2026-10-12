using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Serves the generated synthetic browser consumer with a fixture-only secure session cookie.</summary>
internal static class ContractConsumer
{
    private const string ScriptFile = "src/frontend/IdentityPlatform.Admin.Contracts/dist/consumer.js";
    private const string ScriptRoute = "/consumer.js";
    private const string ConsumerRoute = "/consumer";
    private const string ScriptMediaType = "text/javascript";
    private const string HtmlMediaType = "text/html";
    private const string RootPath = "/";
    private const string Page = "<!doctype html><html lang=\"en\"><title>Synthetic consumer</title><script src=\"/consumer.js\"></script><body>Contract consumer</body></html>";

    /// <summary>Map only fixed synthetic assets and a cookie-setting page used by browser transport regressions.</summary>
    internal static void Map(WebApplication app)
    {
        var script = File.ReadAllText(Path.Combine(ContractFixtureConfiguration.RepositoryRoot(), ScriptFile));
        app.MapGet(ScriptRoute, () => Results.Content(script, ScriptMediaType)).ExcludeFromDescription();
        app.MapGet(ConsumerRoute, (HttpContext context) =>
        {
            context.Response.Cookies.Append(ContractValues.CookieName, ContractValues.CookieValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = RootPath
            });
            return Results.Content(Page, HtmlMediaType);
        }).ExcludeFromDescription();
    }
}
