using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Checks the emitted native JSON envelope and credential-bearing fields before any bytes are admitted to the backchannel.</summary>
internal static class DeviceResponse
{
    private const string Media = "application/json";
    private const string NoStore = "no-store";

    /// <summary>Compares the bounded scalar response map with every body field from the actual native HTTP formatter.</summary>
    internal static void Require(ProtocolResponseCapture capture, DeviceNative native)
    {
        var response = native.Transaction.Response ?? throw ProtocolStorageOwner.Unavailable();
        var expected = response.GetParameters().ToDictionary(pair => pair.Key, pair => (JsonElement)pair.Value, StringComparer.Ordinal);
        var status = response.Error switch
        {
            null => StatusCodes.Status200OK,
            OpenIddictConstants.Errors.InvalidClient => StatusCodes.Status401Unauthorized,
            OpenIddictConstants.Errors.ServerError => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest
        };
        if (capture.StatusCode != status || !MediaTypeHeaderValue.TryParse(capture.Headers.ContentType.ToString(), out var media) ||
            !string.Equals(media.MediaType.Value, Media, StringComparison.OrdinalIgnoreCase) ||
            !capture.Headers.CacheControl.ToString().Contains(NoStore, StringComparison.OrdinalIgnoreCase) ||
            capture.Headers.SetCookie.Count != 0 || capture.Headers.Location.Count != 0 || expected.Count == 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        using var json = JsonDocument.Parse(capture.Bytes);
        foreach (var field in json.RootElement.EnumerateObject())
        {
            if (!Allowed(native.Transaction.EndpointType, response.Error, field.Name) || !expected.Remove(field.Name, out var value) ||
                !JsonElement.DeepEquals(field.Value, value))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
        if (expected.Count != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Permits only documented RFC grant fields for the specific issuance or refusal being formatted.</summary>
    private static bool Allowed(OpenIddictServerEndpointType endpoint, string? error, string name) => error is not null ?
        name is OpenIddictConstants.Parameters.Error or OpenIddictConstants.Parameters.ErrorDescription or OpenIddictConstants.Parameters.ErrorUri :
        endpoint == OpenIddictServerEndpointType.DeviceAuthorization ?
            name is OpenIddictConstants.Parameters.DeviceCode or OpenIddictConstants.Parameters.UserCode or OpenIddictConstants.Parameters.VerificationUri or
                OpenIddictConstants.Parameters.Interval or OpenIddictConstants.Parameters.ExpiresIn :
            name is OpenIddictConstants.Parameters.AccessToken or OpenIddictConstants.Parameters.TokenType or OpenIddictConstants.Parameters.ExpiresIn or
                OpenIddictConstants.Parameters.IdToken or OpenIddictConstants.Parameters.Scope;
}
