using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Completes only current-session recovery-code regeneration after native unused-factor verification.</summary>
/// <example><code>// Follow the fixed verification location after the recovery-code regeneration password form.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class RegenerateModel(IAdministratorSecurityJourney journey) : PageModel
{
    public string? Interaction
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

    /// <summary>Renders a locator-only form without creating or invalidating any recovery code.</summary>
    /// <example><code>// GET /account/administrator/security/recovery-codes/verify?Interaction=OPAQUE_LOCATOR.</code></example>
    public IActionResult OnGet()
    {
        Interaction = AdministratorBrowser.Locator(HttpContext);
        return Interaction is null ? BadRequest() : Page();
    }

    /// <summary>Renders codes exactly once after native generation, invalidation, audit and replacement-session issuance.</summary>
    /// <example><code>// POST the current authenticator code and native antiforgery token.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        RecoveryCodes = await journey.CompleteAsync(HttpContext, regeneration: true);
        if (RecoveryCodes is not null)
        {
            return Page();
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
