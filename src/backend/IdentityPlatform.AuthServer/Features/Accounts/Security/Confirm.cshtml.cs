using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Confirms only a native-authorized replacement; voluntary journeys also require their exact current session.</summary>
/// <example><code>// GET the fixed confirmation location returned by recovery or current-factor step-up.</code></example>
public sealed class ConfirmModel(IAdministratorSecurityJourney journey) : PageModel
{
    public string? Interaction
    {
        get; private set;
    }
    public string? Seed
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }
    public IReadOnlyList<string>? RecoveryCodes
    {
        get; private set;
    }

    /// <summary>Displays only the exact bound staged key without extending password, step-up or interaction freshness.</summary>
    /// <example><code>// GET /account/administrator/security/replacement/confirm?Interaction=OPAQUE_LOCATOR.</code></example>
    public async Task<IActionResult> OnGetAsync()
    {
        Interaction = AdministratorBrowser.Locator(HttpContext);
        Seed = await journey.StagedAsync(HttpContext);
        return Seed is null ? BadRequest() : Page();
    }

    /// <summary>Releases the new code collection only after exact-destination installation, audit and session issuance.</summary>
    /// <example><code>// POST the displayed new-authenticator code and native antiforgery token.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        RecoveryCodes = await journey.CompleteAsync(HttpContext, regeneration: false);
        if (RecoveryCodes is not null)
        {
            return Page();
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
