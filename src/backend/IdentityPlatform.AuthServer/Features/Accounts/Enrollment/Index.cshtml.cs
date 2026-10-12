using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Enrollment;

/// <summary>Renders protected bootstrap enrollment and the single successful recovery-code response.</summary>
/// <example><code>// POST the native antiforgery-protected form at /account/administrator/enrollment.</code></example>
public sealed class IndexModel(IAdministratorJourney journey) : PageModel
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

    /// <summary>Renders an initial password form or reads existing bound staging without allocating authority.</summary>
    /// <example><code>// GET /account/administrator/enrollment; render only this feature’s permitted account view.</code></example>
    public async Task<IActionResult> OnGetAsync()
    {
        if (Request.Query.Count == 0)
        {
            return Page();
        }
        Interaction = AdministratorBrowser.Locator(HttpContext);
        Seed = await journey.StagedAsync(HttpContext);
        return Seed is null ? BadRequest() : Page();
    }

    /// <summary>Accepts exactly one password or confirmation form and never redisplays submitted credentials.</summary>
    /// <example><code>// POST /account/administrator/enrollment; submit the rendered antiforgery-protected form.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (form.ContainsKey(AdministratorBrowser.Interaction))
        {
            RecoveryCodes = await journey.CompleteAsync(HttpContext, enrollment: true);
            if (RecoveryCodes is not null)
            {
                return Page();
            }
        }
        else
        {
            var location = await journey.BeginAsync(HttpContext, enrollment: true);
            if (location is not null)
            {
                return LocalRedirect(location);
            }
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
