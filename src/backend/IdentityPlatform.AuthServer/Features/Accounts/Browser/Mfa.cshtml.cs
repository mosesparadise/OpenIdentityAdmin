using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Completes only the current request-bound login interaction, never standalone, enrollment, recovery or pilot authority.</summary>
/// <example><code>// Follow the admitted browser login's fixed MFA locator, then submit its native protected code form.</code></example>
public sealed class MfaModel(IServiceProvider services) : PageModel
{
    public string? Intent
    {
        get; private set;
    }
    public string? Interaction
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }

    /// <summary>Renders an exact two-field locator only with the same protected issuer-browser entry proof.</summary>
    /// <example><code>// GET the dedicated browser MFA route with the original Intent and admitted Interaction.</code></example>
    public IActionResult OnGet()
    {
        var intent = BrowserAccountRequest.Intent(HttpContext, mfa: true);
        Intent = intent is null ? null : services.GetRequiredService<BrowserAccountProof>().Read(HttpContext, intent)?.Intent;
        Interaction = Intent is null ? null : Request.Query[AdministratorBrowser.Interaction][0];
        return Interaction is null ? BadRequest() : Page();
    }

    /// <summary>Returns only a completion selector to the fixed configured BFF after actual MFA, audited commit and one-shot issuer cookie publication.</summary>
    /// <example><code>// POST Intent, Interaction, Code and the native antiforgery token to the fixed browser MFA route.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var location = await services.GetRequiredService<BrowserAccountJourney>().CompleteAsync(HttpContext);
        if (location is not null)
        {
            return Redirect(location);
        }
        var form = await BrowserAccountRequest.FormAsync(HttpContext, services.GetRequiredService<BrowserAccountProfile>(),
            BrowserAccountProfile.Intent, AdministratorBrowser.Interaction, AdministratorBrowser.Code);
        if (form is not null && CeremonySecrets.IsIdentifier(form[AdministratorBrowser.Interaction][0]))
        {
            Intent = services.GetRequiredService<BrowserAccountProof>().Read(HttpContext, form[BrowserAccountProfile.Intent][0]!)?.Intent;
            Interaction = Intent is null ? null : form[AdministratorBrowser.Interaction][0];
        }
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}
