using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Pilot;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public sealed class IndexModel(LoginInteractions interactions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Interaction
    {
        get; set;
    }
    [BindProperty]
    public string? Username
    {
        get; set;
    }
    [BindProperty]
    public string? Password
    {
        get; set;
    }
    public bool Unavailable
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }

    public async Task OnGetAsync()
    {
        var interaction = await interactions.BoundAsync(HttpContext, Interaction);
        Unavailable = interaction is null || interaction.CompletedSessionId is not null;
        if (Unavailable)
        {
            Response.StatusCode = 400;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var password = Password ?? "";
        Password = null;
        ModelState.Remove(nameof(Password));
        var interaction = await interactions.BoundAsync(HttpContext, Interaction);
        if (interaction is null || interaction.CompletedSessionId is not null)
        {
            Unavailable = true;
            Response.StatusCode = 400;
            return Page();
        }
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(password))
        {
            Error = "Enter your username and password.";
            return Page();
        }
        var previous = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var session = await interactions.CompleteAsync(interaction, Username, password,
            previous.Principal?.FindFirstValue(PilotProfile.SessionClaim), HttpContext.RequestAborted);
        if (session is null)
        {
            Error = "Unable to sign in. Check your details or start a new sign-in request.";
            return Page();
        }
        HttpContext.RequestAborted.ThrowIfCancellationRequested();
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, session.UserId));
        identity.AddClaim(new Claim(PilotProfile.SessionClaim, session.Id));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                IssuedUtc = DateTimeOffset.FromUnixTimeSeconds(session.AuthenticatedAt),
                ExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt)
            });
        return LocalRedirect("/account/complete?interaction=" + interaction.Id);
    }
}
