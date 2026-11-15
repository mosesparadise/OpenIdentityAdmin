using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Admin.Bff.Features.ApplicationGrants;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Forwards one closed retirement intent and requires an exact application/key/job-bound acceptance before publication.</summary>
internal static class ApplicationRetirementTransport
{
    private const string Bearer = "Bearer";
    private const string JsonType = "application/json";
    private const string IfMatch = "If-Match";
    private const string Separator = "/";
    private const int RevisionOffset = 66;

    /// <summary>Uses the existing fixed redirect-free client and original cancellation budget, with no credential forwarding from browser headers or retry.</summary>
    internal static async Task<ApplicationRetirementProxyResult> SendAsync(HttpClient client, Uri target, BrowserAccessCredential access,
        ApplicationMutationRequest input, CancellationToken token)
    {
        if (input.Action != ApplicationProxyAction.Retire || input.Id is null || input.RequestKey is null)
        {
            throw new InvalidDataException();
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target, input.Path));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonType));
        request.Headers.TryAddWithoutValidation(IfMatch, input.Version);
        request.Headers.TryAddWithoutValidation(GrantRevocationRules.KeyHeader, input.RequestKey);
        request.Content = new ReadOnlyMemoryContent(input.Body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonType);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.PreconditionRequired)
        {
            return new((int)response.StatusCode, null);
        }
        return await ParseAsync(response, input, token);
    }

    /// <summary>Rejects malformed framing and distinguishes the terminal application version from the independently advancing job ETag.</summary>
    private static async Task<ApplicationRetirementProxyResult> ParseAsync(HttpResponseMessage response, ApplicationMutationRequest input, CancellationToken token)
    {
        if (response.StatusCode != HttpStatusCode.Accepted || response.Content.Headers.ContentType?.MediaType != JsonType ||
            response.Content.Headers.ContentEncoding.Count != 0 || response.Content.Headers.ContentLength > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ApplicationJson.ReadAsync(stream, ApplicationProjection.MaximumResponseBytes, token);
        if (response.Content.Headers.ContentLength is { } length && length != bytes.Length)
        {
            throw new InvalidDataException();
        }
        var value = ApplicationJson.Parse<ApplicationRetirementAcceptance>(bytes);
        var revision = long.Parse(input.Version!.AsSpan(RevisionOffset, input.Version!.Length - RevisionOffset - 1), CultureInfo.InvariantCulture);
        var path = ApplicationMutationRequest.ApiPath + Separator + Uri.EscapeDataString(input.Id!) + GrantRevocationRules.RouteSuffix + Separator;
        if (value.Job is null || value.ApplicationId != input.Id || value.RequestKey != input.RequestKey || revision == long.MaxValue ||
            !ApplicationVersion.Matches(input.Version, input.Id!, revision) || !ApplicationVersion.Matches(value.ApplicationVersion, input.Id!, revision + 1) ||
            !GrantProxyResult.Valid(value.Job, input.Id!) || response.Headers.ETag?.ToString() != value.Job.Version ||
            response.Headers.Location?.OriginalString != path + value.Job.Id || value.RetiredAt.Offset != TimeSpan.Zero ||
            value.RetiredAt.Ticks % TimeSpan.TicksPerSecond != 0 || value.RetiredAt != value.Job.CreatedAt)
        {
            throw new InvalidDataException();
        }
        return new(StatusCodes.Status202Accepted, value);
    }
}

/// <summary>Contains only validated finite retirement metadata and sanitized downstream status.</summary>
internal sealed record ApplicationRetirementProxyResult(int Status, ApplicationRetirementAcceptance? Acceptance);
