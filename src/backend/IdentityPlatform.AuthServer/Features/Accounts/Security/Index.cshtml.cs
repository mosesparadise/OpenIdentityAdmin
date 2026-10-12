using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Displays only the authenticated account's current session status.</summary>
/// <example><code>// GET /account/administrator/security with a committed administrator cookie.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class IndexModel : PageModel
{
    public DateTimeOffset PasswordVerifiedAt
    {
        get; private set;
    }
    public DateTimeOffset FactorVerifiedAt
    {
        get; private set;
    }
    public DateTimeOffset ExpiresAt
    {
        get; private set;
    }

    /// <summary>Uses this request's authoritative validation result rather than browser claims for authentication times.</summary>
    /// <example><code>// GET /account/administrator/security; render only this feature’s permitted account view.</code></example>
    public IActionResult OnGet()
    {
        if (Request.Query.Count != 0 || HttpContext.Items[AdministratorCookieEvents.StatusKey] is not JourneySession status)
        {
            return Unauthorized();
        }
        PasswordVerifiedAt = DateTimeOffset.FromUnixTimeSeconds(status.PasswordVerifiedAt);
        FactorVerifiedAt = DateTimeOffset.FromUnixTimeSeconds(status.FactorVerifiedAt);
        ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(status.ExpiresAt);
        return Page();
    }
}
