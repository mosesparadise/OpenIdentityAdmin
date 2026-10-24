using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Renders only a protected request-bound administrator password form; existing authentication never bypasses it.</summary>
/// <example><code>// GET the fixed browser login route with one opaque Intent, then submit its native protected form.</code></example>
public sealed class LoginModel(IServiceProvider services) : PageModel
{
    public string? Intent
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }

    /// <summary>Creates only browser proof cookies, with no database admission, authenticated session or automatic challenge.</summary>
    /// <example><code>// GET /account/administrator/browser/login?Intent=opaque-selector</code></example>
    public IActionResult OnGet()
    {
        var intent = BrowserAccountRequest.Intent(HttpContext, mfa: false);
        Intent = intent is null ? null : services.GetRequiredService<BrowserAccountProof>().Prepare(HttpContext, intent)?.Intent;
        return Intent is null ? BadRequest() : Page();
    }

    /// <summary>Runs explicit password admission and permits only unchanged original pending-entry retry after denial.</summary>
    /// <example><code>// POST the rendered Intent, Username, Password and native antiforgery token to the fixed route.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var location = await services.GetRequiredService<BrowserAccountJourney>().BeginAsync(HttpContext);
        if (location is not null)
        {
            return LocalRedirect(location);
        }
        var form = await BrowserAccountRequest.FormAsync(HttpContext, services.GetRequiredService<BrowserAccountProfile>(),
            BrowserAccountProfile.Intent, AdministratorBrowser.Username, AdministratorBrowser.Password);
        Intent = form is null ? null : services.GetRequiredService<BrowserAccountProof>().Read(HttpContext, form[BrowserAccountProfile.Intent][0]!)?.Intent;
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
