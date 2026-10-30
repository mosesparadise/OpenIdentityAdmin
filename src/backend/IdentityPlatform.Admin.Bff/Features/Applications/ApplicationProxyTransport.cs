using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Sends one bounded request to the fixed HTTPS API with only the protected session's credential.</summary>
internal sealed class ApplicationProxyTransport : IDisposable
{
    private const string RootPath = "/";
    private const string InvalidDestination = "Invalid management API destination.";
    private const string ApiPath = "/api/v1/applications";
    private const string JsonType = "application/json";
    private const string Bearer = "Bearer";
    private const int HeaderKilobytes = 16;
    private readonly Uri target;
    private readonly HttpClient client;

    /// <summary>Rejects noncanonical destinations and owns a redirect-free, cookie-free transport with no automatic retries.</summary>
    internal ApplicationProxyTransport(Uri origin)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length != 0 || origin.Query.Length != 0 ||
            origin.Fragment.Length != 0 || origin.AbsolutePath != RootPath)
        {
            throw new ArgumentException(InvalidDestination);
        }
        target = new(origin, ApiPath);
        client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, MaxResponseHeadersLength = HeaderKilobytes })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>Injects only a retained credential and finite encoded query, then projects the bounded response without forwarding headers.</summary>
    internal async Task<(int Status, ApplicationPage? Page)> ReadAsync(BrowserAccessCredential access, ApplicationListQuery query, CancellationToken token)
    {
        var uri = QueryHelpers.AddQueryString(target.AbsoluteUri, new Dictionary<string, string?>
        {
            [ApplicationListQuery.SearchParameter] = query.Search,
            [ApplicationListQuery.PageParameter] = query.Page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [ApplicationListQuery.SizeParameter] = query.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonType));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ((int)response.StatusCode, null);
        }
        if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != JsonType ||
            response.Content.Headers.ContentLength > ApplicationProjection.MaximumResponseBytes)
        {
            return (StatusCodes.Status503ServiceUnavailable, null);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ApplicationJson.ReadAsync(stream, ApplicationProjection.MaximumResponseBytes, token);
        var page = ApplicationJson.Parse<ApplicationPage>(bytes);
        return IsValid(page, query) ? (StatusCodes.Status200OK, page) : (StatusCodes.Status503ServiceUnavailable, null);
    }

    /// <summary>Requires the requested page and exact finite safe records, without trusting upstream schema annotations.</summary>
    private static bool IsValid(ApplicationPage? page, ApplicationListQuery query) => page is not null && page.Page == query.Page &&
        page.PageSize == query.PageSize && page.Items is not null && page.Items.Count <= query.PageSize &&
        (!page.HasNextPage || page.Items.Count == query.PageSize) && page.Items.All(item => item is not null && ApplicationProjection.IsValid(item)) &&
        page.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == page.Items.Count;

    private const string IfMatchHeader = "If-Match";

    /// <summary>Forwards only code-owned method/path/media, current server bearer and canonical precondition; neither cookies nor retries are enabled.</summary>
    internal async Task<ApplicationProxyResult> WriteAsync(BrowserAccessCredential access, ApplicationMutationRequest mutation, CancellationToken token)
    {
        using var request = new HttpRequestMessage(mutation.Method, new Uri(target, mutation.Path));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonType));
        if (mutation.Version is { } version)
        {
            request.Headers.TryAddWithoutValidation(IfMatchHeader, version);
        }
        request.Content = new ReadOnlyMemoryContent(mutation.Body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonType);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.PreconditionRequired)
        {
            return new((int)response.StatusCode, null, null, null);
        }
        var expected = mutation.Action == ApplicationProxyAction.Create ? HttpStatusCode.Created : HttpStatusCode.OK;
        if (response.StatusCode != expected || response.Content.Headers.ContentType?.MediaType != JsonType || response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength > ApplicationProjection.MaximumResponseBytes || response.Headers.ETag?.ToString() is not { } tag || !ApplicationVersion.IsCanonical(tag))
        {
            return new(StatusCodes.Status503ServiceUnavailable, null, null, null);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ApplicationJson.ReadAsync(stream, ApplicationProjection.MaximumResponseBytes, token);
        return ApplicationProxyResult.Parse(bytes, tag, mutation, (int)expected);
    }

    /// <summary>Disposes pooled transport when the owning host stops; response/request owners are joined by each awaited read.</summary>
    public void Dispose() => client.Dispose();
}
