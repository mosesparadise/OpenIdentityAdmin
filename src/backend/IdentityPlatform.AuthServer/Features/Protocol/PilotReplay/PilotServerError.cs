using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Checks the native generation error representation without synthesizing or releasing a staged credential response.</summary>
internal static class PilotServerError
{
    private const string JsonContentType = "application/json;charset=UTF-8";

    /// <summary>Requires exact finite native error fields and excludes browser authority channels before publication.</summary>
    internal static bool Allows(ProtocolResponseCapture capture, OpenIddictResponse response)
    {
        if (!capture.IsSealed || response.Error != OpenIddictConstants.Errors.ServerError ||
            capture.Headers.ContentType != JsonContentType || capture.Headers.ContainsKey(HeaderNames.SetCookie) ||
            capture.Headers.ContainsKey(HeaderNames.Location))
        {
            return false;
        }
        var expected = response.GetParameters();
        if (expected.Any(pair => pair.Key is not (OpenIddictConstants.Parameters.Error or OpenIddictConstants.Parameters.ErrorDescription or
            OpenIddictConstants.Parameters.ErrorUri) || (JsonElement)pair.Value is not { ValueKind: JsonValueKind.String }))
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(capture.Bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            var fields = document.RootElement.EnumerateObject().ToArray();
            return fields.Length == expected.Count && fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length &&
                fields.All(field => field.Value.ValueKind == JsonValueKind.String && expected.TryGetValue(field.Name, out var value) &&
                    field.Value.GetString() == ((JsonElement)value).GetString());
        }
        catch (JsonException) { return false; }
    }
}
