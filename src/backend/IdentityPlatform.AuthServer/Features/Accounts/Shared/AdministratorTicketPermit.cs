using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Allows only the scoped postcommit journey to issue its exact principal once.</summary>
internal sealed class AdministratorTicketPermit
{
    internal const string SessionClaim = "administrator-session";
    private ClaimsPrincipal? expected;
    private long expiresAt;

    /// <summary>Creates a minimal opaque ticket only from the committed result after cancellation and lifetime checks.</summary>
    internal async Task IssueAsync(HttpContext context, JourneyCompletion.Grant grant)
    {
        context.RequestAborted.ThrowIfCancellationRequested();
        grant.Claim();
        var identity = new ClaimsIdentity(AccountSchemes.Administrator);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, grant.UserId));
        identity.AddClaim(new Claim(SessionClaim, grant.SessionId));
        var principal = new ClaimsPrincipal(identity);
        expected = principal;
        expiresAt = grant.ExpiresAt;
        try
        {
            await context.SignInAsync(AccountSchemes.Administrator, principal, new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(grant.ExpiresAt)
            });
        }
        finally { expected = null; }
    }

    /// <summary>Rejects arbitrary, reused, persistent or lifetime-altered signing calls, including correctly protected synthetic tickets.</summary>
    internal void Consume(CookieSigningInContext context)
    {
        if (expected is null || !ReferenceEquals(expected, context.Principal) || context.Properties.IsPersistent ||
            context.Properties.AllowRefresh != false || context.Properties.ExpiresUtc?.ToUnixTimeSeconds() != expiresAt)
        {
            const string Unavailable = "Administrator session issuance is unavailable.";
            throw new InvalidOperationException(Unavailable);
        }
        expected = null;
    }
}
