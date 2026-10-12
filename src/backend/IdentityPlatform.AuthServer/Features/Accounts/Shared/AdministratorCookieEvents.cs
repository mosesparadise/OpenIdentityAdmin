using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Resolves every account request against durable current authority rather than trusting a protected ticket alone.</summary>
internal sealed class AdministratorCookieEvents(JourneySessions sessions, AdministratorTicketPermit permit) : CookieAuthenticationEvents
{
    private const int IdentifierClaims = 2;
    internal static readonly object StatusKey = new();

    /// <summary>Consumes the one-shot postcommit signing permission and rejects all other issuance paths.</summary>
    public override Task SigningIn(CookieSigningInContext context)
    {
        permit.Consume(context);
        return Task.CompletedTask;
    }

    /// <summary>Rejects malformed tickets and unavailable or stale database authority without a cached fallback.</summary>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var claims = principal?.Claims.ToArray();
        if (principal?.Identities.Count() != 1 || principal.Identity?.AuthenticationType != AccountSchemes.Administrator ||
            claims?.Length != IdentifierClaims || claims.Count(claim => claim.Type == ClaimTypes.NameIdentifier) != 1 ||
            claims.Count(claim => claim.Type == AdministratorTicketPermit.SessionClaim) != 1)
        {
            context.RejectPrincipal();
            return;
        }
        var status = await sessions.ValidateAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
            principal.FindFirstValue(AdministratorTicketPermit.SessionClaim)!, context.HttpContext.RequestAborted);
        if (status is null)
        {
            context.RejectPrincipal();
            return;
        }
        context.ShouldRenew = false;
        context.HttpContext.Items[StatusKey] = status;
    }

    /// <summary>Returns explicit authentication failure without redirecting into pilot or protocol journeys.</summary>
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>Returns an explicit authorization failure without granting broader management authority.</summary>
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
