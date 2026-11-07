using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.ApplicationGrants;

/// <summary>Freezes a closed grant action with exact route, original bytes and finite preconditions; no arbitrary proxy destination is accepted.</summary>
internal sealed class GrantProxyRequest
{
    private const string CurrentSegment = ".";
    private const string ParentSegment = "..";

    private const string JsonUtf8 = "application/json; charset=utf-8";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";
    private const int TokenCharacters = 4096;
    private const string Separator = "/";
    internal string Id
    {
        get;
    }
    internal string? JobId
    {
        get;
    }
    internal bool Mutation
    {
        get;
    }
    internal string? Version
    {
        get;
    }
    internal string? Key
    {
        get;
    }
    internal ReadOnlyMemory<byte> Body
    {
        get;
    }
    internal string Path => Location(JobId) + (Mutation && JobId is not null ? GrantRevocationRules.ContinueSuffix : string.Empty);

    /// <summary>Retains only validated original request contents; construction is private to the strict admission method.</summary>
    private GrantProxyRequest(string id, string? jobId, bool mutation, string? version, string? key, byte[] body)
    {
        Id = id;
        JobId = jobId;
        Mutation = mutation;
        Version = version;
        Key = key;
        Body = body;
    }

    /// <summary>Uses the shared exact-origin/bearer-rejection policy and allows only a canonical application/job path.</summary>
    internal static async Task<GrantProxyRequest> ReadAsync(HttpContext http, string id, string? jobId, bool mutation)
    {
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is CurrentSegment or ParentSegment ||
            jobId is not null && !GrantRevocationRules.TryKey(jobId, out _) || !ApplicationBrowserRequest.Origin(http, required: mutation))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var suffix = Resource(id, jobId) + (mutation && jobId is not null ? GrantRevocationRules.ContinueSuffix : string.Empty);
        if (!ApplicationBrowserRequest.Exact(http, ApplicationProxyEndpoint.Path + suffix) ||
            http.Request.Method != (mutation ? HttpMethods.Post : HttpMethods.Get) || http.Request.Headers.ContainsKey(TransferEncoding) ||
            http.Request.Headers.ContainsKey(ContentEncoding) || http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        if (mutation)
        {
            return await WriteAsync(http, id, jobId);
        }
        if (http.Request.ContentLength is not (null or 0) || http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            !await BrowserLoginRequest.EmptyAsync(http))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        return new(id, jobId, false, null, null, []);
    }

    /// <summary>Requires one native CSRF token, strong precondition and canonical scoped key before reading bounded strict JSON.</summary>
    private static async Task<GrantProxyRequest> WriteAsync(HttpContext http, string id, string? jobId)
    {
        var request = http.Request;
        if (request.ContentType is not (ApplicationMutationRequest.JsonType or JsonUtf8) || request.ContentLength is < 0 or > ApplicationEditorRules.MaximumRequestBytes ||
            request.Headers[BrowserRequestPolicy.JsonAntiforgeryHeader] is not { Count: 1 } csrf || csrf[0] is not { Length: > 0 and <= TokenCharacters } ||
            request.Headers[GrantRevocationRules.KeyHeader] is not { Count: 1 } keys || !GrantRevocationRules.TryKey(keys[0], out _))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        if (request.Headers.IfMatch.Count == 0)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status428PreconditionRequired);
        }
        if (request.Headers.IfMatch.Count != 1 || !ApplicationVersion.IsCanonical(request.Headers.IfMatch[0]))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status412PreconditionFailed);
        }
        try
        {
            var bytes = await ApplicationJson.ReadAsync(request.Body, ApplicationEditorRules.MaximumRequestBytes, http.RequestAborted);
            if (request.ContentLength is { } length && length != bytes.Length ||
                GrantRevocationRules.Reason(ApplicationJson.Parse<GrantRevocation>(bytes), jobId is not null) is null)
            {
                throw new InvalidDataException();
            }
            return new(id, jobId, true, request.Headers.IfMatch[0], keys[0], bytes);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Constructs the fixed API status location, including validation of any identifier supplied in the response.</summary>
    internal string Location(string? jobId) => ApplicationMutationRequest.ApiPath + Resource(Id, jobId);

    /// <summary>Encodes only one opaque application segment; job identifiers and all operation suffixes are canonical and code-owned.</summary>
    private static string Resource(string id, string? jobId) => Separator + Uri.EscapeDataString(id) + GrantRevocationRules.RouteSuffix +
        (jobId is null ? string.Empty : Separator + jobId);
}
