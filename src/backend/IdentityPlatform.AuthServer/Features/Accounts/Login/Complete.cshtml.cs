using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public sealed class CompleteModel(LoginInteractions interactions, PilotEligibility eligibility) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Interaction
    {
        get; set;
    }
    public string? ContinueUri
    {
        get; private set;
    }

    public async Task OnGetAsync()
    {
        var interaction = await interactions.BoundAsync(HttpContext, Interaction);
        var cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var session = await eligibility.SessionAsync(cookie.Principal?.FindFirstValue(PilotProfile.SessionClaim),
            cookie.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);
        if (session is null || interaction is null || interaction.CompletedSessionId != session.Id || interaction.CompletedUserId != session.UserId)
        {
            Response.StatusCode = 400;
            return;
        }
        ContinueUri = LoginInteractions.Resume(interaction);
    }
}
