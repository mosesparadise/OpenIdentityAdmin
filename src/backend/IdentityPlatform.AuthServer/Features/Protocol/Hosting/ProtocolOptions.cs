using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Composes a single native server's explicit profile union without enabling absent profile capabilities.</summary>
internal static class ProtocolOptions
{
    internal const string AuthorizePath = "/connect/authorize";
    internal const string TokenPath = "/connect/token";
    internal const string KeysPath = "/connect/jwks";
    internal const string ParPath = "/connect/par";

    /// <summary>Preserves the fixed S256/query/code profile and enables interactive prompts and Basic only for the pilot.</summary>
    internal static void Configure(OpenIddictServerOptions options, ProtocolProfiles profiles)
    {
        options.RequestTokenLifetime = TimeSpan.FromSeconds(profiles.Pilot
            ? PilotProfile.InteractionSeconds : AdministratorRegistrationPolicy.ProofLifetimeSeconds);
        options.CodeChallengeMethods.Clear();
        options.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
        options.PromptValues.Clear();
        options.PromptValues.Add(OpenIddictConstants.PromptValues.None);
        options.ResponseModes.Clear();
        options.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
        options.ClientAuthenticationMethods.Clear();
        options.Scopes.Clear();
        options.Scopes.Add(OpenIddictConstants.Scopes.OpenId);
        if (profiles.Pilot)
        {
            options.PromptValues.Add(OpenIddictConstants.PromptValues.Login);
            options.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic);
            options.Scopes.Add(PilotProfile.Scope);
        }
        if (profiles.Administrator is not null)
        {
            options.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt);
            options.Scopes.Add(AdministratorProtocolIdentity.Scope);
        }
    }
}
