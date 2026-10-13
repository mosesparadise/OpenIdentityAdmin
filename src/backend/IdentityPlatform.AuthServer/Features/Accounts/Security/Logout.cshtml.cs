using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Offers inert logout confirmation and an exact-session audited POST with native antiforgery protection.</summary>
/// <example><code>// GET /account/administrator/logout, then submit the confirmation form.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class LogoutModel(IAdministratorSecurityJourney journey) : PageModel
{
    /// <summary>Displays confirmation without revoking authority or clearing the cookie.</summary>
    /// <example><code>// GET /account/administrator/logout does not sign the user out.</code></example>
    public IActionResult OnGet() => Request.Query.Count == 0 ? Page() : BadRequest();

    /// <summary>Clears only the named local cookie after its stored session revocation and audit commit.</summary>
    /// <example><code>// POST the rendered native antiforgery token; success returns to the fixed administrator login page.</code></example>
    public async Task<IActionResult> OnPostAsync() => await journey.LogoutAsync(HttpContext)
        ? LocalRedirect(AdministratorBrowser.LoginPath) : BadRequest();
}
