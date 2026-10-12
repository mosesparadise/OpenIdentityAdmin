using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using IdentityPlatform.AuthServer.Features.Accounts.Shared;

namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Shares the exact password-only admission behavior across fixed account-security entry pages.</summary>
/// <remarks>Concrete pages choose their action in code and retain native Razor antiforgery and named authorization where required.</remarks>
/// <example><code>// A recovery entry page derives with AdministratorSecurityAction.Recovery; no request chooses the action.</code></example>
public abstract class SecurityPasswordPage(IAdministratorSecurityJourney journey, AdministratorSecurityAction action) : PageModel
{
    public string? Error
    {
        get; private set;
    }

    /// <summary>Renders a query-free form without allocating interactions, changing factors or refreshing authentication.</summary>
    /// <example><code>// GET /account/administrator/recovery renders only the password form.</code></example>
    public IActionResult OnGet() => Request.Query.Count == 0 ? Page() : BadRequest();

    /// <summary>Admits the concrete page's fixed action or displays a generic denial without submitted credential reflection.</summary>
    /// <example><code>// POST the rendered password form with native antiforgery; only a local opaque continuation is returned.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var location = await journey.BeginAsync(HttpContext, action);
        if (location is not null)
        {
            return LocalRedirect(location);
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
