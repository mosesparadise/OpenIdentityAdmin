using System.Collections.Frozen;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Rejects unplanned credentials and identity fields before native success formatting and final issuance commit.</summary>
internal static class NativeAccountResponse
{
    private const int MaximumTokenSeconds = 300;
    private const int ScopeCount = 2;
    private static readonly FrozenSet<string> TokenFields = new[] { OpenIddictConstants.Parameters.AccessToken,
        OpenIddictConstants.Parameters.IdToken, OpenIddictConstants.Parameters.TokenType, OpenIddictConstants.Parameters.ExpiresIn,
        OpenIddictConstants.Parameters.Scope }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> AuthorizationFields = new[] { OpenIddictConstants.Parameters.Code,
        OpenIddictConstants.Parameters.State, OpenIddictConstants.Parameters.Iss }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Requires the exact endpoint-specific native output and its original credential/request bindings.</summary>
    internal static bool Allows(OpenIddictServerEvents.ProcessSignInContext context) => context.EndpointType switch
    {
        OpenIddictServerEndpointType.PushedAuthorization => NativeParResponse.Allows(context.Response),
        OpenIddictServerEndpointType.Authorization => Authorization(context),
        OpenIddictServerEndpointType.Token => Token(context),
        _ => false
    };

    /// <summary>Allows only the code and native issuer/state parameters on the independently verified fixed callback.</summary>
    private static bool Authorization(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var response = context.Response;
        return response.GetParameters().Keys.All(AuthorizationFields.Contains) &&
            String(response, OpenIddictConstants.Parameters.Code, context.AuthorizationCode) &&
            String(response, OpenIddictConstants.Parameters.Iss, (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri) &&
            (string.IsNullOrEmpty(context.Request.State) ? !response.HasParameter(OpenIddictConstants.Parameters.State) :
                String(response, OpenIddictConstants.Parameters.State, context.Request.State));
    }

    /// <summary>Requires the complete native code-exchange response, with no optional additional credential or identity fields.</summary>
    private static bool Token(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var response = context.Response;
        var parameters = response.GetParameters();
        if (!TokenFields.SetEquals(parameters.Keys) || !String(response, OpenIddictConstants.Parameters.AccessToken, context.AccessToken) ||
            !String(response, OpenIddictConstants.Parameters.IdToken, context.IdentityToken) ||
            !String(response, OpenIddictConstants.Parameters.TokenType, OpenIddictConstants.TokenTypes.Bearer) ||
            !parameters.TryGetValue(OpenIddictConstants.Parameters.ExpiresIn, out var lifetime))
        {
            return false;
        }
        var value = (JsonElement)lifetime;
        var scope = (JsonElement)parameters[OpenIddictConstants.Parameters.Scope];
        var scopes = scope.ValueKind == JsonValueKind.String ? scope.GetString()?.Split(' ') : null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds) && seconds is > 0 and <= MaximumTokenSeconds &&
            context.AccessTokenPrincipal?.GetExpirationDate() is { } expiration && context.AccessTokenPrincipal.GetCreationDate() is { } creation &&
            seconds <= Math.Ceiling((expiration - creation).TotalSeconds) && scopes is { Length: ScopeCount } &&
            scopes.Contains(OpenIddictConstants.Scopes.OpenId, StringComparer.Ordinal) && scopes.Contains(AdministratorProtocolIdentity.Scope, StringComparer.Ordinal);
    }

    /// <summary>Requires one actual scalar string; native convenience conversions cannot normalize arrays or missing values into approval.</summary>
    private static bool String(OpenIddictResponse response, string name, string? expected) => !string.IsNullOrEmpty(expected) &&
        response.GetParameters().TryGetValue(name, out var parameter) && (JsonElement)parameter is { ValueKind: JsonValueKind.String } value && value.GetString() == expected;
}
