using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Validates exact native error headers so removed response fields cannot survive in a header or cookie.</summary>
internal static class NativeErrorHeaders
{
    private const string NoStore = "no-store";
    private const string NoCache = "no-cache";
    private const string Basic = "Basic";
    private const string Quote = "\"";
    private const string EscapedQuote = "\\\"";

    /// <summary>Preserves preexisting host headers and permits only the native finite denial representation.</summary>
    internal static bool Allows(ProtocolResponseCapture capture, Dictionary<string, string?[]> baseline,
        OpenIddictServerEvents.BaseRequestContext context, NativeErrorFormat format)
    {
        var native = context.Transaction.Response!;
        var expectedStatus = format == NativeErrorFormat.Query ? StatusCodes.Status302Found : native.Error switch
        {
            OpenIddictConstants.Errors.InvalidClient => StatusCodes.Status401Unauthorized,
            OpenIddictConstants.Errors.ServerError => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest
        };
        return capture.StatusCode == expectedStatus && capture.ReasonPhrase is null && !capture.Headers.ContainsKey(HeaderNames.SetCookie) &&
            capture.Headers.CacheControl == NoStore && capture.Headers.Pragma == NoCache && capture.Headers.Expires == NativeErrorPolicy.ExpiredCache &&
            capture.Headers.All(pair => IsNative(pair.Key) || baseline.TryGetValue(pair.Key, out var values) && pair.Value.ToArray().SequenceEqual(values)) &&
            baseline.All(pair => IsNative(pair.Key) || capture.Headers.TryGetValue(pair.Key, out var values) && values.ToArray().SequenceEqual(pair.Value)) &&
            Challenge(capture, context, format) && Representation(capture, context, format);
    }

    /// <summary>Requires the exact native Basic challenge for invalid_client; there are no extra parameters or multiple challenges.</summary>
    private static bool Challenge(ProtocolResponseCapture capture, OpenIddictServerEvents.BaseRequestContext context, NativeErrorFormat format)
    {
        var response = context.Transaction.Response!;
        if (format != NativeErrorFormat.Json || response.Error != OpenIddictConstants.Errors.InvalidClient)
        {
            return !capture.Headers.ContainsKey(HeaderNames.WWWAuthenticate);
        }
        var expected = Basic + string.Join(',', response.GetParameters().Select(pair => $" {pair.Key}=\"{((string?)pair.Value)?.Replace(Quote, EscapedQuote)}\""));
        return capture.Headers.WWWAuthenticate.Count == 1 && capture.Headers.WWWAuthenticate[0] == expected;
    }

    /// <summary>Checks local/JSON body metadata or the exact callback and independently bound query fields.</summary>
    private static bool Representation(ProtocolResponseCapture capture, OpenIddictServerEvents.BaseRequestContext context, NativeErrorFormat format)
    {
        if (format != NativeErrorFormat.Query)
        {
            return !capture.Headers.ContainsKey(HeaderNames.Location) && capture.Headers.ContentType ==
                (format == NativeErrorFormat.Json ? NativeErrorPolicy.JsonContentType : NativeErrorPolicy.TextContentType) &&
                capture.Headers.ContentLength == capture.Bytes.Length;
        }
        if (context is not OpenIddictServerEvents.ApplyAuthorizationResponseContext authorization || capture.Headers.Location.Count != 1 ||
            !Uri.TryCreate(capture.Headers.Location[0], UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Path) != new Uri(authorization.RedirectUri!).GetLeftPart(UriPartial.Path) ||
            !string.IsNullOrEmpty(uri.Fragment) || !capture.Bytes.IsEmpty || capture.Headers.ContainsKey(HeaderNames.ContentType) || capture.Headers.ContainsKey(HeaderNames.ContentLength))
        {
            return false;
        }
        var query = QueryHelpers.ParseQuery(uri.Query);
        var response = context.Transaction.Response!.GetParameters();
        var configured = QueryHelpers.ParseQuery(new Uri(authorization.RedirectUri!).Query);
        return !configured.Keys.Any(response.ContainsKey) && query.Count == response.Count + configured.Count &&
            configured.All(pair => query.TryGetValue(pair.Key, out var values) && values.SequenceEqual(pair.Value)) &&
            response.All(pair => query.TryGetValue(pair.Key, out var value) && value.Count == 1 && value[0] == (string?)pair.Value);
    }

    /// <summary>Lists only headers written by the pinned formatter pipeline; all other headers must exactly match the host baseline.</summary>
    private static bool IsNative(string name) => name.Equals(HeaderNames.CacheControl, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(HeaderNames.Pragma, StringComparison.OrdinalIgnoreCase) || name.Equals(HeaderNames.Expires, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase) || name.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(HeaderNames.WWWAuthenticate, StringComparison.OrdinalIgnoreCase) || name.Equals(HeaderNames.Location, StringComparison.OrdinalIgnoreCase);
}
