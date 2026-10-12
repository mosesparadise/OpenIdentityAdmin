using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Mfa;

/// <summary>Completes login only with the interaction's native unused authenticator proof.</summary>
/// <example><code>// The login page redirects to /account/administrator/mfa with its opaque locator.</code></example>
public sealed class IndexModel(IAdministratorJourney journey) : PageModel
{
    public string? Interaction
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }

    /// <summary>Renders a locator-bound form without granting authority or modifying server state.</summary>
    /// <example><code>// GET /account/administrator/mfa; render only this feature’s permitted account view.</code></example>
    public IActionResult OnGet()
    {
        Interaction = AdministratorBrowser.Locator(HttpContext);
        return Interaction is null ? BadRequest() : Page();
    }

    /// <summary>Redirects only after a committed session is issued; failures never reflect the submitted code.</summary>
    /// <example><code>// POST /account/administrator/mfa; submit the rendered antiforgery-protected form.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var result = await journey.CompleteAsync(HttpContext, enrollment: false);
        if (result is not null)
        {
            return LocalRedirect(AdministratorBrowser.SecurityPath);
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
