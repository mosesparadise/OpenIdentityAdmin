using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Formats only subject and the complete consented current email pair, with independent exact-output checks.</summary>
internal sealed class UserInfoResponse
{
    private const string ContentType = "application/json; charset=utf-8";
    private const string NoStore = "no-store";
    private const string NoCache = "no-cache";
    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly Projection projection;
    private readonly byte[] bytes;
    private readonly Dictionary<string, string?[]> headers;
    private readonly ProtocolResponseCapture capture;

    /// <summary>Freezes the finite projection and existing host headers before application formatting or response callbacks execute.</summary>
    internal UserInfoResponse(HttpContext http, string subject, ManagedEmailProjection? email)
    {
        capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        projection = new(subject, email?.Address, email?.Verified);
        bytes = JsonSerializer.SerializeToUtf8Bytes(projection, Options);
        headers = capture.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        if (capture.HasStarted || !capture.Bytes.IsEmpty || capture.Headers.ContainsKey(HeaderNames.SetCookie))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Executes the Minimal API JSON result inside the owned response child, never on live transport.</summary>
    internal async Task FormatAsync(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers.Pragma = NoCache;
        http.Response.Headers.Expires = NativeErrorPolicy.ExpiredCache;
        http.Response.ContentLength = bytes.Length;
        await Results.Json(projection, Options, ContentType, StatusCodes.Status200OK).ExecuteAsync(http);
        RequireCurrent(http);
    }

    /// <summary>Rejects extra data, headers, cookies, response replacement and all callback mutations before the read transaction can commit.</summary>
    internal void RequireCurrent(HttpContext http)
    {
        if (!ReferenceEquals(capture, http.Features.Get<IHttpResponseFeature>()) || !ReferenceEquals(capture, http.Features.Get<IHttpResponseBodyFeature>()) ||
            capture.StatusCode != StatusCodes.Status200OK || capture.ReasonPhrase is not null || !capture.Bytes.Span.SequenceEqual(bytes) ||
            capture.Headers.ContentType != ContentType || capture.Headers.ContentLength != bytes.Length || capture.Headers.CacheControl != NoStore ||
            capture.Headers.Pragma != NoCache || capture.Headers.Expires != NativeErrorPolicy.ExpiredCache ||
            capture.Headers.ContainsKey(HeaderNames.SetCookie) || capture.Headers.ContainsKey(HeaderNames.WWWAuthenticate) || capture.Headers.ContainsKey(HeaderNames.Location) ||
            capture.Headers.Any(pair => !Owned(pair.Key) && (!headers.TryGetValue(pair.Key, out var original) || !pair.Value.ToArray().SequenceEqual(original))) ||
            headers.Any(pair => !Owned(pair.Key) && (!capture.Headers.TryGetValue(pair.Key, out var actual) || !actual.ToArray().SequenceEqual(pair.Value))))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Allows only the five response headers written by this formatter; every host header must remain byte-for-byte unchanged.</summary>
    private static bool Owned(string name) => name.Equals(HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase) || name.Equals(HeaderNames.CacheControl, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(HeaderNames.Pragma, StringComparison.OrdinalIgnoreCase) || name.Equals(HeaderNames.Expires, StringComparison.OrdinalIgnoreCase);

    /// <summary>Names the entire public wire contract; nullable email fields are omitted together and verification remains a JSON boolean.</summary>
    private sealed record Projection([property: JsonPropertyName("sub")] string Subject,
        [property: JsonPropertyName("email")] string? Email, [property: JsonPropertyName("email_verified")] bool? Verified);
}
