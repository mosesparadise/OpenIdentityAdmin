using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Uses the existing fixed API transport owner for exactly one validated catalogue send with no forwarding of browser headers.</summary>
internal static class CatalogueTransport
{
    private const string Bearer = "Bearer";
    private const string JsonType = "application/json";
    private const string IfMatch = "If-Match";

    /// <summary>Owns request/response disposal and actual bounded JSON reads before validating finite output against the original request.</summary>
    internal static async Task<CatalogueProxyResult> SendAsync(HttpClient client, Uri target, BrowserAccessCredential access,
        CatalogueProxyRequest input, CancellationToken token)
    {
        using var request = new HttpRequestMessage(input.Method, new Uri(target, input.ApiPath));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonType));
        if (input.Mutation)
        {
            if (input.Version is not null)
            {
                request.Headers.TryAddWithoutValidation(IfMatch, input.Version);
            }
            request.Content = new ReadOnlyMemoryContent(input.Body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonType);
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.PreconditionRequired)
        {
            return new((int)response.StatusCode);
        }
        var expected = input.Action == CatalogueProxyAction.Create ? HttpStatusCode.Created : input.Action == CatalogueProxyAction.Delete ? HttpStatusCode.Accepted : HttpStatusCode.OK;
        if (response.StatusCode != expected || response.Content.Headers.ContentType?.MediaType != JsonType || response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength is < 0 or > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ApplicationJson.ReadAsync(stream, ApplicationProjection.MaximumResponseBytes, token);
        if (response.Content.Headers.ContentLength is { } length && bytes.Length != length)
        {
            throw new InvalidDataException();
        }
        return CatalogueProxyResult.Parse(bytes, input, (int)expected, response.Headers.ETag?.ToString(), response.Headers.Location?.OriginalString);
    }
}
