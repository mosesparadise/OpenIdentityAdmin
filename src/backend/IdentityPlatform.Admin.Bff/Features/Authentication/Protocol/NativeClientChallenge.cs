using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Requires the fixed noninteractive profile after native negotiation and before durable state creation.</summary>
internal sealed class NativeClientChallenge(NativeClientProfile profile) : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    private const string RedirectionEndpoint = "redirection";

    /// <summary>Supplies the mandatory noninteractive prompt only when absent; caller overrides must already match the contract.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        if (!context.Parameters.ContainsKey(OpenIddictConstants.Parameters.Prompt))
        {
            context.Parameters.Add(OpenIddictConstants.Parameters.Prompt, OpenIddictConstants.PromptValues.None);
        }
        Require(context, profile);
        return ValueTask.CompletedTask;
    }

    /// <summary>Rejects all profile, scope, challenge and destination alternatives before a prepared state may be committed.</summary>
    internal static void Require(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile)
    {
        NativeClientMetadata.Require(context.Configuration, profile);
        if (context.Registration.RegistrationId != AdministratorProtocolIdentity.ClientId || context.Registration.ClientId != AdministratorProtocolIdentity.ClientId ||
            context.Registration.Issuer?.OriginalString != profile.Issuer.AbsoluteUri || context.Registration.RedirectUri?.OriginalString != profile.Callback.AbsoluteUri ||
            context.Registration.DisablePushedAuthorizationRequests || context.Registration.ClientSecret is not null ||
            context.Options.ClientUri?.OriginalString != profile.ClientUri.AbsoluteUri ||
            !context.Registration.ClientAuthenticationMethods.SetEquals([OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt]) ||
            context.ClientId != AdministratorProtocolIdentity.ClientId || context.RedirectUri != profile.Callback.AbsoluteUri ||
            context.GrantType != OpenIddictConstants.GrantTypes.AuthorizationCode || context.ResponseType != OpenIddictConstants.ResponseTypes.Code ||
            context.ResponseMode != OpenIddictConstants.ResponseModes.Query || context.CodeChallengeMethod != OpenIddictConstants.CodeChallengeMethods.Sha256 ||
            !context.Scopes.SetEquals([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]) ||
            context.Audiences.Count != 0 || context.Resources.Count != 0 || context.LoginHint is not null || context.IdentityTokenHint is not null ||
            string.IsNullOrEmpty(context.Nonce) || string.IsNullOrEmpty(context.RequestForgeryProtection) || string.IsNullOrEmpty(context.CodeVerifier) ||
            context.CodeChallenge != Hash(context.CodeVerifier) || !ParametersAllowed(context) || !BrowserLoginReservation.DemandMatches(context) ||
            NativeBrowserChallenge.Find(context) is { } browser && (!profile.Registration.IsBrowserSession || !browser.Matches(context)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks that native protected-state claims retain every prepared challenge binding before any manager write.</summary>
    internal static void RequireState(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile)
    {
        Require(context, profile);
        var principal = context.StateTokenPrincipal;
        if (principal is null || principal.GetClaim(OpenIddictConstants.Claims.Private.Issuer) != profile.ClientUri.AbsoluteUri ||
            principal.GetClaim(OpenIddictConstants.Claims.AuthorizationServer) != profile.Issuer.AbsoluteUri ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId) != context.Registration.RegistrationId ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RedirectUri) != context.RedirectUri ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.GrantType) != context.GrantType ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.ResponseType) != context.ResponseType ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.Nonce) != context.Nonce ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.CodeVerifier) != context.CodeVerifier ||
            principal.GetClaim(OpenIddictConstants.Claims.RequestForgeryProtection) != context.RequestForgeryProtection ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.EndpointType) != RedirectionEndpoint ||
            !principal.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals(context.Scopes) ||
            !(NativeBrowserChallenge.Find(context) is { } browser ? browser.StateMatches(context) : NativeBrowserChallenge.UnboundState(context)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Allows only exact prompt=none and an optional bounded nonnegative integer max_age; custom native field overwrites are forbidden.</summary>
    private static bool ParametersAllowed(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        foreach (var (key, value) in context.Parameters)
        {
            if (NativeBrowserChallenge.IsBindingParameter(key) && NativeBrowserChallenge.Find(context) is { } browser && browser.Matches(context))
            {
                continue;
            }
            if (key == OpenIddictConstants.Parameters.Prompt && (string?)value == OpenIddictConstants.PromptValues.None)
            {
                continue;
            }
            if (key == OpenIddictConstants.Parameters.MaxAge && (string?)value is { } age &&
                age.Length > 0 && age.All(char.IsAsciiDigit) && int.TryParse(age, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                continue;
            }
            return false;
        }
        return context.Parameters.ContainsKey(OpenIddictConstants.Parameters.Prompt);
    }

    /// <summary>Uses the native protocol's base64url SHA-256 transformation for both PKCE and outbound nonce binding.</summary>
    internal static string Hash(string value) => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
