using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const int BufferBytes = 8192;
    private const int MaximumDepth = 16;
    private readonly Uri target;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = MaximumDepth
    };

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
        var bytes = await ReadBoundedAsync(response.Content, token);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaximumDepth });
        RequireUnique(document.RootElement);
        var page = JsonSerializer.Deserialize<ApplicationPage>(bytes, Json);
        return IsValid(page, query) ? (StatusCodes.Status200OK, page) : (StatusCodes.Status503ServiceUnavailable, null);
    }

    /// <summary>Bounds actual decoded bytes independently of Content-Length and never exposes a partial body.</summary>
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken token)
    {
        await using var stream = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[BufferBytes];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > ApplicationProjection.MaximumResponseBytes)
            {
                throw new InvalidDataException();
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    /// <summary>Rejects duplicate JSON keys before the serializer can choose a misleading last value.</summary>
    private static void RequireUnique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException();
                }
                RequireUnique(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                RequireUnique(item);
            }
        }
    }

    /// <summary>Requires the requested page and exact finite safe records, without trusting upstream schema annotations.</summary>
    private static bool IsValid(ApplicationPage? page, ApplicationListQuery query) => page is not null && page.Page == query.Page &&
        page.PageSize == query.PageSize && page.Items is not null && page.Items.Count <= query.PageSize &&
        (!page.HasNextPage || page.Items.Count == query.PageSize) && page.Items.All(item => item is not null && ApplicationProjection.IsValid(item)) &&
        page.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == page.Items.Count;

    /// <summary>Disposes pooled transport when the owning host stops; response/request owners are joined by each awaited read.</summary>
    public void Dispose() => client.Dispose();
}
