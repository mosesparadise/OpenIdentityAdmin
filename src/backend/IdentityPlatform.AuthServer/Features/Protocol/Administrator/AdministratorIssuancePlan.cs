using System.Collections.Frozen;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Matches the exact native generation flags to the fixed administrator profile's planned persisted row delta.</summary>
internal sealed class AdministratorIssuancePlan
{
    internal AdministratorTokenCreation Creation
    {
        get;
    }
    internal IReadOnlySet<string> TokenTypes
    {
        get;
    }

    /// <summary>Owns a fixed set of expected native token types; callers cannot widen a plan after reservation.</summary>
    private AdministratorIssuancePlan(AdministratorTokenCreation creation, params string[] types)
    {
        Creation = creation;
        TokenTypes = types.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>Rejects caching, extra credentials and omitted or nonreturned planned output before the first native create.</summary>
    internal static AdministratorIssuancePlan? Read(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Principal is null || context.ClientId != AdministratorProtocolIdentity.ClientId || context.Request.ClientId != AdministratorProtocolIdentity.ClientId ||
            context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped ||
            context.GenerateDeviceCode || context.IncludeDeviceCode || context.GenerateUserCode || context.IncludeUserCode ||
            context.GenerateRefreshToken || context.IncludeRefreshToken || context.GenerateIssuedToken || context.IncludeIssuedToken)
        {
            return null;
        }
        var flags = (context.GenerateRequestToken, context.IncludeRequestToken, context.GenerateAuthorizationCode, context.IncludeAuthorizationCode,
            context.GenerateAccessToken, context.IncludeAccessToken, context.GenerateIdentityToken, context.IncludeIdentityToken);
        return (context.EndpointType, flags) switch
        {
            (OpenIddictServerEndpointType.PushedAuthorization, (true, true, false, false, false, false, false, false)) =>
                new(AdministratorTokenCreation.PushedRequest, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken),
            (OpenIddictServerEndpointType.Authorization, (false, false, true, true, false, false, false, false))
                when context.Request.IsAuthorizationCodeFlow() && !string.IsNullOrEmpty(context.Request.RequestUri) &&
                    string.IsNullOrEmpty(context.Principal.GetAuthorizationId()) =>
                new(AdministratorTokenCreation.AuthorizationCode, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode),
            (OpenIddictServerEndpointType.Token, (false, false, false, false, true, true, true, true))
                when context.Request.IsAuthorizationCodeGrantType() && !string.IsNullOrEmpty(context.Principal.GetAuthorizationId()) =>
                new(AdministratorTokenCreation.TokenExchange, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken),
            _ => null
        };
    }
}
