using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Checks browser provenance and exact transport targets without trusting forwarded headers or route aliases.</summary>
internal static class ApplicationBrowserRequest
{
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    private const string OriginHeader = "Origin";
    private const string FetchSite = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";
    private const string NoSite = "none";
    private const string Authorization = "Authorization";

    /// <summary>Requires exact configured origin for mutations; same-origin token GETs may omit the browser's Origin header.</summary>
    internal static bool Origin(HttpContext http, bool required)
    {
        var request = http.Request;
        var origin = http.RequestServices.GetRequiredService<BrowserSessionDestinations>().Origin;
        return request.IsHttps && !request.Headers.ContainsKey(Authorization) &&
            (request.Headers.TryGetValue(OriginHeader, out var incoming) ? incoming.Count == 1 && incoming[0] == origin : !required) &&
            (!request.Headers.TryGetValue(FetchSite, out var site) || site.Count == 1 && (site[0] == SameOrigin || !required && site[0] == NoSite));
    }

    /// <summary>Rejects encoded route aliases and any query, including empty trailing query delimiters.</summary>
    internal static bool Exact(HttpContext http, string path) => http.Request.Path.ToUriComponent() == path && !http.Request.QueryString.HasValue &&
        (http.Features.Get<IHttpRequestFeature>()?.RawTarget is not { Length: > 0 } raw || raw == path);

    /// <summary>Requires an exact bodyless token acquisition with no native form/header validation shortcut.</summary>
    internal static async Task<bool> TokenAsync(HttpContext http) => Origin(http, required: false) && Exact(http, ApplicationAntiforgeryEndpoint.Path) &&
        HttpMethods.IsGet(http.Request.Method) && http.Request.ContentLength is null or 0 &&
        !http.Request.Headers.ContainsKey(TransferEncoding) && !http.Request.Headers.ContainsKey(ContentEncoding) &&
        !http.Request.Headers.ContainsKey(BrowserRequestPolicy.JsonAntiforgeryHeader) &&
        http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true && await BrowserLoginRequest.EmptyAsync(http);
}
