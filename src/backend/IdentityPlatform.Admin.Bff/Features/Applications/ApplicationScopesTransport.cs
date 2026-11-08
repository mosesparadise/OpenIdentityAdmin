using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Sends exactly one fixed application scope request and validates the bounded native assignment response before browser publication.</summary>
internal static class ApplicationScopesTransport
{
    private const string Separator = "/";
    private const string Bearer = "Bearer";
    private const string JsonType = "application/json";
    private const string IfMatch = "If-Match";
    private const int RevisionStart = 66;

    /// <summary>Retains only the server credential, canonical precondition and validated bytes; no redirects, cookies or mutation retries are introduced.</summary>
    internal static async Task<ApplicationScopesResult> SendAsync(HttpClient client, Uri target, BrowserAccessCredential access,
        string id, ApplicationMutationRequest? mutation, CancellationToken token)
    {
        using var request = new HttpRequestMessage(mutation is null ? HttpMethod.Get : HttpMethod.Put,
            new Uri(target.AbsoluteUri + Separator + Uri.EscapeDataString(id) + ApplicationScopeRules.Suffix));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonType));
        if (mutation is not null)
        {
            if (mutation.Action != ApplicationProxyAction.AssignScopes || mutation.Id != id)
            {
                throw new InvalidDataException();
            }
            request.Headers.TryAddWithoutValidation(IfMatch, mutation.Version);
            request.Content = new ReadOnlyMemoryContent(mutation.Body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonType);
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
            HttpStatusCode.Conflict || mutation is not null && response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.PreconditionRequired)
        {
            return new((int)response.StatusCode, null, null);
        }
        return await ParseAsync(response, id, mutation, token);
    }

    /// <summary>Rejects malformed framing, foreign resource tags, assignment substitution and anything other than the exact successor version.</summary>
    private static async Task<ApplicationScopesResult> ParseAsync(HttpResponseMessage response, string id, ApplicationMutationRequest? mutation, CancellationToken token)
    {
        var tag = response.Headers.ETag?.ToString();
        if (response.StatusCode != HttpStatusCode.OK || response.Headers.Location is not null || !ApplicationVersion.IsCanonical(tag) ||
            response.Content.Headers.ContentType?.MediaType != JsonType || response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ApplicationJson.ReadAsync(stream, ApplicationProjection.MaximumResponseBytes, token);
        if (response.Content.Headers.ContentLength is { } length && bytes.Length != length)
        {
            throw new InvalidDataException();
        }
        var output = ApplicationJson.Parse<ApplicationScopes>(bytes);
        var revision = long.Parse(tag!.AsSpan(RevisionStart, tag!.Length - RevisionStart - 1), System.Globalization.CultureInfo.InvariantCulture);
        if (!ApplicationScopeRules.IsValid(output, id) || output.ExpiresAt.Offset != TimeSpan.Zero ||
            !ApplicationVersion.Matches(tag, id, revision) || mutation is not null && !mutation.Matches(output, revision))
        {
            throw new InvalidDataException();
        }
        return new(StatusCodes.Status200OK, output, tag);
    }
}

/// <summary>Retains only finite assignment state and the exact upstream aggregate version.</summary>
internal sealed record ApplicationScopesResult(int Status, ApplicationScopes? Output, string? Version);
