using System.Text;
using IdentityPlatform.AuthServer.Features.Pilot;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Chooses only the unambiguous enabled pilot transport; native extraction/authentication independently validates it later.</summary>
internal static class PilotTransportIdentity
{
    private const string BasicPrefix = "Basic ";
    private const string FormType = "application/x-www-form-urlencoded";
    private const int MaximumHeaderLength = 8192;

    /// <summary>Reads only method-appropriate bounded form/query input after account request protections have run.</summary>
    internal static async ValueTask<bool> MatchesAsync(HttpContext http, OpenIddictServerEndpointType endpoint)
    {
        if (endpoint is not (OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token))
        {
            return false;
        }
        IEnumerable<KeyValuePair<string, StringValues>> fields;
        if (endpoint == OpenIddictServerEndpointType.Authorization && HttpMethods.IsGet(http.Request.Method))
        {
            fields = http.Request.Query;
        }
        else if (HttpMethods.IsPost(http.Request.Method) && http.Request.HasFormContentType &&
            http.Request.ContentType!.StartsWith(FormType, StringComparison.OrdinalIgnoreCase))
        {
            fields = await http.Request.ReadFormAsync(http.RequestAborted);
        }
        else
        {
            return false;
        }
        return Matches(fields, http.Request.Headers.Authorization, endpoint);
    }

    /// <summary>Rejects duplicate and case-aliased fields and mixed proof methods before selecting an unowned legacy transport.</summary>
    internal static bool Matches(IEnumerable<KeyValuePair<string, StringValues>> input, StringValues authorization, OpenIddictServerEndpointType endpoint)
    {
        var fields = input.ToArray();
        if (fields.Any(pair => pair.Value.Count != 1) || fields.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length ||
            fields.Any(pair => IsAlias(pair.Key)) || fields.Any(pair => pair.Key is OpenIddictConstants.Parameters.ClientAssertion or
                OpenIddictConstants.Parameters.ClientAssertionType or OpenIddictConstants.Parameters.ClientSecret))
        {
            return false;
        }
        var client = fields.SingleOrDefault(pair => pair.Key == OpenIddictConstants.Parameters.ClientId).Value;
        if (endpoint == OpenIddictServerEndpointType.Authorization)
        {
            return authorization.Count == 0 && client.Count == 1 && client[0] == PilotProfile.ClientId;
        }
        return authorization.Count == 1 && BasicClient(authorization[0]) == PilotProfile.ClientId &&
            (client.Count == 0 || client.Count == 1 && client[0] == PilotProfile.ClientId);
    }

    /// <summary>Rejects aliases of security-relevant identity/proof fields even when a case-insensitive form collection retained only one spelling.</summary>
    private static bool IsAlias(string key) => new[] { OpenIddictConstants.Parameters.ClientId, OpenIddictConstants.Parameters.ClientSecret,
        OpenIddictConstants.Parameters.ClientAssertion, OpenIddictConstants.Parameters.ClientAssertionType }.Any(name =>
            string.Equals(name, key, StringComparison.OrdinalIgnoreCase) && name != key);

    /// <summary>Matches native ASCII/Base64 and form-style identity decoding without retaining or authenticating the submitted secret.</summary>
    private static string? BasicClient(string? header)
    {
        if (header is null || header.Length > MaximumHeaderLength || !header.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        try
        {
            var decoded = Encoding.ASCII.GetString(Convert.FromBase64String(header[BasicPrefix.Length..]));
            var separator = decoded.IndexOf(':');
            return separator < 0 ? null : Uri.UnescapeDataString(decoded[..separator].Replace('+', ' '));
        }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
