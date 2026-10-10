using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Token;

public static class TokenEndpoint
{
    public static async Task<IResult> HandleAsync(HttpContext context, PilotEligibility eligibility,
        IOpenIddictAuthorizationManager authorizations)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Validated request required.");
        if (!request.IsAuthorizationCodeGrantType())
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.UnsupportedGrantType);
        }
        if (!ProofKeyPolicy.IsVerifier(request.CodeVerifier))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.InvalidGrant);
        }
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        if (!result.Succeeded || principal is null ||
            await eligibility.SessionAsync(principal.GetClaim(PilotProfile.SessionClaim), principal.GetClaim(OpenIddictConstants.Claims.Subject), context.RequestAborted) is null ||
            !await eligibility.RegistrationAsync(request.ClientId, principal.GetScopes(), context.RequestAborted) ||
            !principal.GetResources().SequenceEqual([PilotProfile.Resource], StringComparer.Ordinal))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.InvalidGrant);
        }
        var authorizationId = principal.GetAuthorizationId();
        var authorization = authorizationId is null ? null : await authorizations.FindByIdAsync(authorizationId, context.RequestAborted);
        if (authorization is null || !await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, context.RequestAborted))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.InvalidGrant);
        }
        // Retain the validated native code principal, tracking, nonce and presenter bindings.
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
