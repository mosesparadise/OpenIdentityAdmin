using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Restricts owned native denials to finite error fields and a validated fixed redirect.</summary>
internal static class NativeErrorPolicy
{
    internal const string AuthorizationDefaultUri = "https://documentation.openiddict.com/errors/ID2015";
    internal const string TokenDefaultUri = "https://documentation.openiddict.com/errors/ID2024";
    internal const string Description = "The request could not be completed.";
    internal const int DenialPropertyCount = 2;
    internal const string JsonContentType = "application/json;charset=UTF-8";
    internal const string TextContentType = "text/plain;charset=UTF-8";
    internal const string ExpiredCache = "Thu, 01 Jan 1970 00:00:00 GMT";

    /// <summary>Allows only standard errors used by the fixed code/PAR profile; arbitrary error text never becomes output.</summary>
    internal static bool Allows(string? error) => error is OpenIddictConstants.Errors.InvalidRequest or OpenIddictConstants.Errors.InvalidClient or
        OpenIddictConstants.Errors.InvalidGrant or OpenIddictConstants.Errors.InvalidScope or OpenIddictConstants.Errors.InvalidToken or
        OpenIddictConstants.Errors.UnauthorizedClient or OpenIddictConstants.Errors.UnsupportedGrantType or OpenIddictConstants.Errors.UnsupportedResponseType or
        OpenIddictConstants.Errors.RequestNotSupported or OpenIddictConstants.Errors.RequestUriNotSupported or OpenIddictConstants.Errors.LoginRequired or
        OpenIddictConstants.Errors.ConsentRequired or OpenIddictConstants.Errors.InteractionRequired or OpenIddictConstants.Errors.AccessDenied or
        OpenIddictConstants.Errors.TemporarilyUnavailable or OpenIddictConstants.Errors.ServerError;

    /// <summary>Requires exact scalar values before any native header or body serialization can expose them.</summary>
    internal static bool Parameters(OpenIddictServerEvents.BaseRequestContext context, string expected, AdministratorRegistrationPolicy policy)
    {
        var response = context.Transaction.Response;
        var error = context.EndpointType == OpenIddictServerEndpointType.Token && expected == OpenIddictConstants.Errors.InvalidToken
            ? OpenIddictConstants.Errors.InvalidGrant : expected;
        if (response is null || !String(response, OpenIddictConstants.Parameters.Error, error) ||
            !String(response, OpenIddictConstants.Parameters.ErrorDescription, Description))
        {
            return false;
        }
        var redirect = context is OpenIddictServerEvents.ApplyAuthorizationResponseContext authorization && !string.IsNullOrEmpty(authorization.RedirectUri);
        if (redirect && !Redirect((OpenIddictServerEvents.ApplyAuthorizationResponseContext)context, policy))
        {
            return false;
        }
        return response.GetParameters().Keys.All(key => key is OpenIddictConstants.Parameters.Error or OpenIddictConstants.Parameters.ErrorDescription ||
            redirect && key is OpenIddictConstants.Parameters.State or OpenIddictConstants.Parameters.Iss) &&
            (!redirect || String(response, OpenIddictConstants.Parameters.Iss, (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri) &&
                (string.IsNullOrEmpty(context.Transaction.Request?.State) ? !response.HasParameter(OpenIddictConstants.Parameters.State) :
                    String(response, OpenIddictConstants.Parameters.State, context.Transaction.Request.State)));
    }

    /// <summary>Allows query redirects only from the actual successful native validation result and the immutable fixed policy callback.</summary>
    private static bool Redirect(OpenIddictServerEvents.ApplyAuthorizationResponseContext context, AdministratorRegistrationPolicy policy)
    {
        return context.RedirectUri == policy.RedirectUri && context.ResponseMode == OpenIddictConstants.ResponseModes.Query &&
            NativeErrorOrigin.Find(context.Transaction)?.AllowsRedirect(context, policy.RedirectUri) == true;
    }

    /// <summary>Rejects arrays/objects and convenience conversions that would hide a different wire value.</summary>
    private static bool String(OpenIddictResponse response, string key, string? expected) => !string.IsNullOrEmpty(expected) &&
        response.GetParameters().TryGetValue(key, out var value) && (JsonElement)value is { ValueKind: JsonValueKind.String } json && json.GetString() == expected;
}
