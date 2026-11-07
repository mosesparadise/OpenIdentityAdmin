using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Freezes one finite browser catalogue request before session observation or any downstream send.</summary>
internal sealed record CatalogueProxyRequest(string Kind, CatalogueProxyAction Action, string? Id, Guid? Job,
    ApplicationListQuery? Query, string? Version, ReadOnlyMemory<byte> Body, object? Input)
{
    private const string JsonType = "application/json";
    private const string JsonUtf8 = "application/json; charset=utf-8";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";
    private const string GuidFormat = "D";
    private const int TokenLength = 4096;
    internal bool Mutation => Action is CatalogueProxyAction.Create or CatalogueProxyAction.Edit or CatalogueProxyAction.Delete;
    internal string ResourcePath => Job is { } job ? CataloguePaths.Job(Kind, job) : Id is not null ? CataloguePaths.Detail(Kind, Id) : CataloguePaths.Collection(Kind);
    internal string ApiPath => Query is null ? CataloguePaths.ApiRoot + ResourcePath : QueryHelpers.AddQueryString(CataloguePaths.ApiRoot + ResourcePath,
        new Dictionary<string, string?>
        {
            [ApplicationListQuery.SearchParameter] = Query.Search,
            [ApplicationListQuery.PageParameter] = Query.Page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ApplicationListQuery.SizeParameter] = Query.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
    internal HttpMethod Method => Action switch
    {
        CatalogueProxyAction.List or CatalogueProxyAction.Details or CatalogueProxyAction.Status => HttpMethod.Get,
        CatalogueProxyAction.Create => HttpMethod.Post,
        CatalogueProxyAction.Edit => HttpMethod.Put,
        CatalogueProxyAction.Delete => HttpMethod.Delete,
        _ => throw new InvalidOperationException()
    };

    /// <summary>Rejects route, method, framing, unknown fields and invalid policy before obtaining any current server credential.</summary>
    internal static async Task<CatalogueProxyRequest> ReadAsync(HttpContext http, string kind, CatalogueProxyAction action, string? id, string? jobId)
    {
        if (!Enum.IsDefined(action) || kind is not (CatalogueVersion.Scope or CatalogueVersion.Audience) ||
            id is not null && !CataloguePaths.IsTarget(id))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var job = JobId(jobId);
        if ((action is CatalogueProxyAction.Details or CatalogueProxyAction.Edit or CatalogueProxyAction.Delete) != (id is not null) ||
            (action == CatalogueProxyAction.Status) != (job is not null))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var query = action == CatalogueProxyAction.List ? ReadQuery(http) : null;
        var input = new CatalogueProxyRequest(kind, action, id, job, query, null, ReadOnlyMemory<byte>.Empty, null);
        await RequireFramingAsync(http, input);
        if (!input.Mutation)
        {
            return input;
        }
        var version = ReadVersion(http, action);
        try
        {
            var bytes = await ApplicationJson.ReadAsync(http.Request.Body, CatalogueRules.MaximumRequestBytes, http.RequestAborted);
            if (http.Request.ContentLength is { } length && length != bytes.Length)
            {
                throw new InvalidDataException();
            }
            return input with
            {
                Version = version,
                Body = bytes,
                Input = Validate(bytes, kind, action)
            };
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Checks same-origin provenance, actual bodylessness for reads and a single bounded native proof header for mutations.</summary>
    private static async Task RequireFramingAsync(HttpContext http, CatalogueProxyRequest input)
    {
        var request = http.Request;
        var path = CataloguePaths.BrowserRoot + input.ResourcePath;
        var raw = path + (input.Query is null ? string.Empty : request.QueryString.Value);
        if (!ApplicationBrowserRequest.Origin(http, input.Mutation) || request.Path.ToUriComponent() != path || request.Method != input.Method.Method ||
            input.Query is null && request.QueryString.HasValue ||
            http.Features.Get<IHttpRequestFeature>()?.RawTarget is { Length: > 0 } actual && actual != raw ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) || http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        if (input.Mutation)
        {
            if (request.ContentType is not (JsonType or JsonUtf8) || request.ContentLength is < 0 or > CatalogueRules.MaximumRequestBytes ||
                request.Headers[BrowserRequestPolicy.JsonAntiforgeryHeader] is not { Count: 1 } proof || proof[0] is not { Length: > 0 and <= TokenLength })
            {
                throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
            }
        }
        else if (request.ContentLength is not (null or 0) || http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            !await BrowserLoginRequest.EmptyAsync(http))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Uses the established bounded paging grammar while retaining only validated query values.</summary>
    private static ApplicationListQuery ReadQuery(HttpContext http) => http.Request.QueryString.Value?.Length > ApplicationProjection.MaximumQueryCharacters
        ? throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest)
        : ApplicationListQuery.Parse(http.Request.Query.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value.ToArray()!)))
            ?? throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);

    /// <summary>Accepts only one canonical nonempty durable status identifier without interpreting it as a URL.</summary>
    private static Guid? JobId(string? text) => text is null ? null : Guid.TryParseExact(text, GuidFormat, out var id) && id != Guid.Empty && id.ToString(GuidFormat) == text
        ? id : throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);

    /// <summary>Separates missing strong preconditions from malformed catalogue tags and rejects create preconditions.</summary>
    private static string? ReadVersion(HttpContext http, CatalogueProxyAction action)
    {
        var values = http.Request.Headers.IfMatch;
        if (action == CatalogueProxyAction.Create)
        {
            return values.Count == 0 ? null : throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        if (values.Count == 0)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status428PreconditionRequired);
        }
        return values.Count == 1 && CatalogueVersion.IsCanonical(values[0]) ? values[0] : throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
    }

    /// <summary>Validates complete finite DTOs independently of API acceptance; unknown and duplicate JSON members have already failed parsing.</summary>
    private static object Validate(byte[] bytes, string kind, CatalogueProxyAction action)
    {
        if (action == CatalogueProxyAction.Delete)
        {
            var input = ApplicationJson.Parse<CatalogueDeleteInput>(bytes);
            return input.Confirmed && Guid.TryParseExact(input.RequestKey, GuidFormat, out var key) && key != Guid.Empty && key.ToString(GuidFormat) == input.RequestKey
                ? input : throw new InvalidDataException();
        }
        if (kind == CatalogueVersion.Scope)
        {
            var input = ApplicationJson.Parse<ScopeInput>(bytes);
            return CatalogueRules.Validate(input).Count == 0 ? input : throw new InvalidDataException();
        }
        var audience = ApplicationJson.Parse<AudienceInput>(bytes);
        return CatalogueRules.Validate(audience).Count == 0 ? audience : throw new InvalidDataException();
    }
}

/// <summary>Restricts catalogue forwarding to explicit reviewed path/method pairs.</summary>
internal enum CatalogueProxyAction
{
    List, Details, Create, Edit, Delete, Status
}
