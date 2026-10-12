using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Mfa;

/// <summary>Starts password verification without issuing administrator authentication.</summary>
/// <example><code>// Submit the native antiforgery-protected login form before completing MFA.</code></example>
public sealed class LoginModel(IAdministratorJourney journey) : PageModel
{
    public string? Error
    {
        get; private set;
    }

    /// <summary>Renders the form only at its exact query-free endpoint without allocating an interaction.</summary>
    /// <example><code>// GET /account/administrator/login; render only this feature’s permitted account view.</code></example>
    public IActionResult OnGet() => Request.Query.Count == 0 ? Page() : BadRequest();

    /// <summary>Admits a restricted browser-bound interaction or returns the same secret-free denial.</summary>
    /// <example><code>// POST /account/administrator/login; submit the rendered antiforgery-protected form.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var location = await journey.BeginAsync(HttpContext, enrollment: false);
        if (location is not null)
        {
            return LocalRedirect(location);
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
