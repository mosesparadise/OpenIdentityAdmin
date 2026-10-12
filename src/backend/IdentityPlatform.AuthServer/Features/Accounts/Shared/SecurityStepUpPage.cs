using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using IdentityPlatform.AuthServer.Features.Accounts.Shared;

namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Shares bound native step-up forms while concrete pages fix recovery-code versus current-factor verification.</summary>
/// <remarks>Native Razor antiforgery remains mandatory and submitted codes are never copied into response models.</remarks>
/// <example><code>// GET /account/administrator/recovery/verify?Interaction=OPAQUE_LOCATOR with its bound browser cookie.</code></example>
public abstract class SecurityStepUpPage(IAdministratorSecurityJourney journey, bool recovery) : PageModel
{
    public string? Interaction
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }

    /// <summary>Renders a canonical locator-only form without spending proof or changing stored authority.</summary>
    /// <example><code>// GET the fixed verification route after password admission.</code></example>
    public IActionResult OnGet()
    {
        Interaction = AdministratorBrowser.Locator(HttpContext);
        return Interaction is null ? BadRequest() : Page();
    }

    /// <summary>Transitions only after actual native proof and audit commit, otherwise renders no submitted secret.</summary>
    /// <example><code>// POST the displayed code form; success redirects to the fixed replacement confirmation route.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var location = await journey.AuthorizeAsync(HttpContext, recovery);
        if (location is not null)
        {
            return LocalRedirect(location);
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
