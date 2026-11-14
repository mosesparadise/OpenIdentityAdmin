using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Admits only native code/query or consent-bound access/ID JSON output, never refresh or administrator authority.</summary>
internal static class ManagedResponseRules
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.Parameters.AccessToken, OpenIddictConstants.Parameters.IdToken, OpenIddictConstants.Parameters.TokenType,
        OpenIddictConstants.Parameters.ExpiresIn, OpenIddictConstants.Parameters.Scope
    };

    /// <summary>Requires exact native output fields and bounded integer lifetime before actual formatting and again before commit.</summary>
    internal static bool Allows(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            return NativeAccountResponse.Allows(context);
        }
        var response = context.Response;
        var parameters = response.GetParameters();
        return context.EndpointType == OpenIddictServerEndpointType.Token && context.Principal is not null && Fields.SetEquals(parameters.Keys) &&
            String(response, OpenIddictConstants.Parameters.AccessToken, context.AccessToken) &&
            String(response, OpenIddictConstants.Parameters.IdToken, context.IdentityToken) &&
            String(response, OpenIddictConstants.Parameters.TokenType, OpenIddictConstants.TokenTypes.Bearer) &&
            String(response, OpenIddictConstants.Parameters.Scope, string.Join(' ', context.Principal.GetScopes())) &&
            (JsonElement)parameters[OpenIddictConstants.Parameters.ExpiresIn] is { ValueKind: JsonValueKind.Number } value &&
            value.TryGetInt64(out var seconds) && seconds is > 0 and <= ManagedLoginRules.LifetimeSeconds &&
            context.AccessTokenPrincipal?.GetExpirationDate() is { } expiration && context.AccessTokenPrincipal.GetCreationDate() is { } creation &&
            seconds <= Math.Ceiling((expiration - creation).TotalSeconds);
    }

    /// <summary>Requires an exact scalar native value; convenience conversions cannot normalize arrays into approval.</summary>
    private static bool String(OpenIddictResponse response, string field, string? expected) => !string.IsNullOrEmpty(expected) &&
        response.GetParameters().TryGetValue(field, out var parameter) && (JsonElement)parameter is { ValueKind: JsonValueKind.String } value && value.GetString() == expected;
}
