using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Renders managed form policy only after the current human operation has validated native request, registration and exact callback under locks.</summary>
internal static class ManagedFormPolicy
{
    private const string Referrer = "Referrer-Policy";
    private const string FormReferrer = "strict-origin";
    private const string InertReferrer = "no-referrer";
    private const string Invalid = "Managed form presentation is unavailable.";

    /// <summary>Allows only the bound callback origin on actionable consent, leaving password/MFA same-origin and all terminal output inert.</summary>
    internal static void Apply(HttpContext context, ManagedHumanRequest request, ManagedHumanResult result)
    {
        if (request.ClientId != result.ClientId)
        {
            throw new InvalidOperationException(Invalid);
        }
        var actionable = result.State is ManagedLoginState.PendingPassword or ManagedLoginState.PendingMfa or ManagedLoginState.PendingConsent;
        var destination = result.State == ManagedLoginState.PendingConsent ? new Uri(request.Callback, UriKind.Absolute) : null;
        AccountRequestGuard.SetFormDestination(context, destination);
        context.Response.Headers[Referrer] = actionable ? FormReferrer : InertReferrer;
    }
}
