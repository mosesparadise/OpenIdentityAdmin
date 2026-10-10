using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Authorize;

public static class AuthorizeEndpoint
{
    public static async Task<IResult> HandleAsync(HttpContext context, LoginInteractions interactions,
        PilotEligibility eligibility, TimeProvider time)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Validated request required.");
        if (!ProofKeyPolicy.IsChallenge(request.CodeChallenge) ||
            !AuthenticationFreshness.TryGetMaximumAge(request, out var maximumAge))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.InvalidRequest);
        }
        if (!await eligibility.RegistrationAsync(request.ClientId, request.GetScopes(), context.RequestAborted))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.AccessDenied);
        }
        var native = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var token = native.Properties?.GetParameter<ClaimsPrincipal>(OpenIddictServerAspNetCoreConstants.Properties.RequestTokenPrincipal);
        var tokenId = token?.GetTokenId();
        if (tokenId is null || request.RequestUri is null || token?.GetExpirationDate() is not { } expiry)
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.InvalidRequest);
        }
        var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var session = await eligibility.SessionAsync(cookie.Principal?.FindFirstValue(PilotProfile.SessionClaim),
            cookie.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), context.RequestAborted);
        var interaction = await interactions.FindAsync(tokenId, context.RequestAborted);
        var freshRequired = session is null || AuthenticationFreshness.Required(request, maximumAge, session.AuthenticatedAt,
            time.GetUtcNow().ToUnixTimeSeconds());
        if (interaction is null)
        {
            if (request.HasPromptValue(OpenIddictConstants.PromptValues.None) && freshRequired)
            {
                return ProtocolError.Create(OpenIddictConstants.Errors.LoginRequired);
            }
            interaction = await interactions.CreateAsync(context, tokenId, request.RequestUri, expiry.ToUnixTimeSeconds());
            return interaction is null ? ProtocolError.Create(OpenIddictConstants.Errors.TemporarilyUnavailable) :
                Results.LocalRedirect(freshRequired ? "/account/login?interaction=" + interaction.Id : LoginInteractions.Resume(interaction));
        }
        interaction = await interactions.BoundAsync(context, interaction.Id);
        if (interaction is null)
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.AccessDenied);
        }
        var completed = interaction.CompletedSessionId is not null;
        if (completed && (session is null || interaction.CompletedSessionId != session.Id || interaction.CompletedUserId != session.UserId))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.AccessDenied);
        }
        if (completed && maximumAge is > 0 && session is not null &&
            time.GetUtcNow().ToUnixTimeSeconds() - session.AuthenticatedAt > maximumAge)
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.LoginRequired);
        }
        if (session is null || freshRequired && !completed)
        {
            return request.HasPromptValue(OpenIddictConstants.PromptValues.None)
                ? ProtocolError.Create(OpenIddictConstants.Errors.LoginRequired)
                : Results.LocalRedirect("/account/login?interaction=" + interaction.Id);
        }
        if (!await interactions.ConsumeAsync(interaction, session, context.RequestAborted))
        {
            return ProtocolError.Create(OpenIddictConstants.Errors.AccessDenied);
        }
        context.Response.Cookies.Delete(PilotProfile.CorrelationPrefix + interaction.Id, LoginInteractions.CookieOptions());
        return Results.SignIn(PilotEligibility.Principal(session), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
