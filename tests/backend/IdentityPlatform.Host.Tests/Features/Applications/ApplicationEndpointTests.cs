using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Proves exact route/framing/query rejection before any missing database or authentication service can be consulted.</summary>
public sealed class ApplicationEndpointTests
{
    private const string NoStore = "no-store";
    private const string ApiPath = "/api/v1/applications";
    private const string BrowserPath = "/bff/api/v1/applications";
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://bff.example.test/";
    private const string Transfer = "Transfer-Encoding";
    private const string Chunked = "chunked";
    private const string Bearer = "Bearer invalid";
    private const string Status = "status";
    private const string Items = "items";
    private const string JsonProblem = "application/problem+json";
    private const int DuplicateParts = 2;

    /// <summary>Unknown, duplicate, noncanonical and oversized queries return a safe problem without touching storage.</summary>
    [Theory]
    [InlineData(false, "?unknown=1")]
    [InlineData(true, "?unknown=1")]
    [InlineData(false, "?page=01")]
    [InlineData(true, "?pageSize=101")]
    [InlineData(false, "?search=a&search=b")]
    [InlineData(true, "?search=a&search=b")]
    [InlineData(false, "?search=%00")]
    [InlineData(true, "?search=%00")]
    public Task Given_InvalidQuery_When_CollectionRuns_Then_SafeBadRequestPrecedesStorage(bool browser, string query) =>
        RequireAsync(browser, http => http.Request.QueryString = new QueryString(query), StatusCodes.Status400BadRequest);

    /// <summary>HTTP, non-GET, encoded route and actual HTTP/2 body signals cannot reach the read operation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_InvalidRequestFraming_When_CollectionRuns_Then_NoReadOrRedirectOccurs(bool browser)
    {
        await RequireAsync(browser, http => http.Request.Scheme = Uri.UriSchemeHttp, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, http => http.Request.Method = HttpMethods.Post, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, http => http.Request.Headers[Transfer] = Chunked, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, http => http.Request.ContentLength = 1, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, http => http.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyPresent()), StatusCodes.Status400BadRequest);
        await RequireAsync(browser, http => http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path.Value!.ToUpperInvariant(), StatusCodes.Status400BadRequest);
    }

    /// <summary>Missing or repeated API bearer headers never borrow a supplied ambient identity or resolve a native manager.</summary>
    [Fact]
    public async Task Given_AbsentOrAmbiguousBearer_When_ApiReadRuns_Then_UnauthorizedProblemIsReturned()
    {
        await RequireAsync(false, _ => { }, StatusCodes.Status401Unauthorized);
        await RequireAsync(false, http => http.Request.Headers.Authorization = Enumerable.Repeat(Bearer, DuplicateParts).ToArray(), StatusCodes.Status401Unauthorized);
    }

    /// <summary>Explicit foreign browser provenance cannot be substituted with a forged bearer header.</summary>
    [Theory]
    [InlineData("Origin", "https://foreign.example.test")]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    public Task Given_ForeignBrowserProvenance_When_ProxyRuns_Then_RequestIsRejected(string header, string value) =>
        RequireAsync(true, http => { http.Request.Headers[header] = value; http.Request.Headers.Authorization = Bearer; }, StatusCodes.Status400BadRequest);

    /// <summary>Missing native dependencies produce only a finite unavailable problem, with no reflected credential or partial output.</summary>
    [Fact]
    public Task Given_MissingNativeDependency_When_AuthenticatedRouteRuns_Then_SafeUnavailableProblemIsReturned() =>
        RequireAsync(false, http => http.Request.Headers.Authorization = Bearer, StatusCodes.Status503ServiceUnavailable);

    /// <summary>Creates only output and framing dependencies, so any accidental storage resolution changes the expected status.</summary>
    private static async Task RequireAsync(bool browser, Action<DefaultHttpContext> change, int expected)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton(TimeProvider.System)
            .AddSingleton(new BrowserSessionDestinations(Issuer, Client)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Method = HttpMethods.Get;
        http.Request.Path = browser ? BrowserPath : ApiPath;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path;
        await using var output = new MemoryStream();
        http.Response.Body = output;
        change(http);
        if (browser)
        {
            await ApplicationProxyEndpoint.HandleAsync(http);
        }
        else
        {
            await ApplicationListEndpoint.HandleAsync(http);
        }
        Assert.Equal(expected, http.Response.StatusCode);
        Assert.Equal(NoStore, http.Response.Headers.CacheControl);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.StartsWith(JsonProblem, http.Response.ContentType, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(output.ToArray());
        Assert.Equal(expected, body.RootElement.GetProperty(Status).GetInt32());
        Assert.False(body.RootElement.TryGetProperty(Items, out _));
        Assert.False(body.RootElement.GetRawText().Contains(Bearer, StringComparison.Ordinal));
    }

    /// <summary>Models the server's body-detection flag independently of content-length headers.</summary>
    private sealed class BodyPresent : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
