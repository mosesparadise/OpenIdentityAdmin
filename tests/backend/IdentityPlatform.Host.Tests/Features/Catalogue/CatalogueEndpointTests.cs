using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Catalogue;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Catalogue;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Catalogue;

/// <summary>Requires exact finite catalogue framing and preconditions before native authentication or any database dependency is resolved.</summary>
public sealed class CatalogueEndpointTests
{
    private const int InitialRevision = 1;
    private const int MismatchedLength = 1;
    private const byte UnexpectedByte = 1;
    private const string Api = "/api/v1/audiences";
    private const string Browser = "/bff/api/v1/audiences";
    private const string Client = "https://bff.example.test/";
    private const string Origin = "https://bff.example.test";
    private const string Issuer = "https://issuer.example.test/";
    private const string Target = "example-audience";
    private const string JsonType = "application/json";
    private const string Input = "{\"resource\":\"orders-api\",\"displayName\":\"Orders\",\"owner\":\"Operations\",\"enabled\":true}";
    private const string Proof = "native-proof-placeholder";
    private const string OriginHeader = "Origin";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string StatusField = "status";
    private const string Separator = "/";

    private const string Compressed = "gzip";
    private const string Chunked = "chunked";
    private const string Bearer = "Bearer invalid";
    private const string Wildcard = "*";
    private const string Weak = "W/";
    private const string Duplicate = "{\"enabled\":true,\"enabled\":false}";

    /// <summary>Finite list query parsing rejects aliases, duplicates, overflows and unknown keys on both HTTP surfaces.</summary>
    [Theory]
    [InlineData(false, "?unknown=1")]
    [InlineData(true, "?unknown=1")]
    [InlineData(false, "?page=01")]
    [InlineData(true, "?page=01")]
    [InlineData(false, "?pageSize=101")]
    [InlineData(true, "?pageSize=101")]
    [InlineData(false, "?search=a&search=b")]
    [InlineData(true, "?search=a&search=b")]
    [InlineData(false, "?search=%00")]
    [InlineData(true, "?search=%00")]
    public Task Given_InvalidCatalogueQuery_When_ReadEndpointRuns_Then_BadRequestPrecedesAnyStorage(bool browser, string query) =>
        RequireAsync(browser, mutation: false, http => http.Request.QueryString = new(query), StatusCodes.Status400BadRequest);

    /// <summary>Actual GET bytes, method substitution, non-TLS, compressed/chunked requests and raw target aliases cannot reach protected storage.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ReadFramingSubstitution_When_CatalogueRuns_Then_ActualBodyAndRouteAreRejected(bool browser)
    {
        await RequireAsync(browser, false, http => http.Request.Body = new MemoryStream([UnexpectedByte]), StatusCodes.Status400BadRequest);
        await RequireAsync(browser, false, http => http.Request.Scheme = Uri.UriSchemeHttp, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, false, http => http.Request.Method = HttpMethods.Post, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, false, http => http.Request.Headers.ContentEncoding = Compressed, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, false, http => http.Request.Headers.TransferEncoding = Chunked, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, false, http => http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path.Value!.ToUpperInvariant(), StatusCodes.Status400BadRequest);
        if (browser)
        {
            await RequireAsync(true, false, http => http.Request.Headers.Authorization = Bearer, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Missing versus malformed catalogue tags keep428 versus400 while finite canonical requests reach independent native authentication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_EditPreconditions_When_MissingMalformedOrCanonical_Then_CatalogueStatusesRemainDistinct(bool browser)
    {
        await RequireAsync(browser, true, http => http.Request.Headers.IfMatch = default, StatusCodes.Status428PreconditionRequired);
        await RequireAsync(browser, true, http => http.Request.Headers.IfMatch = Wildcard, StatusCodes.Status400BadRequest);
        await RequireAsync(browser, true, http => http.Request.Headers.IfMatch = Weak + CatalogueVersion.Create(CatalogueVersion.Audience, Target, InitialRevision), StatusCodes.Status400BadRequest);
        await RequireAsync(browser, true, http => http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Duplicate)), StatusCodes.Status400BadRequest);
        await RequireAsync(browser, true, http => http.Request.ContentLength = MismatchedLength, StatusCodes.Status400BadRequest);
        if (!browser)
        {
            await RequireAsync(false, true, _ => { }, StatusCodes.Status401Unauthorized);
        }
        else
        {
            await RequireAsync(true, true, http => http.Request.Headers.Remove(Csrf), StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Only presentation and fixed origin dependencies are supplied, so accidentally reaching native storage cannot satisfy these expected denials.</summary>
    private static async Task RequireAsync(bool browser, bool mutation, Action<DefaultHttpContext> change, int expected)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton(TimeProvider.System)
            .AddSingleton(new BrowserSessionDestinations(Issuer, Client)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Method = mutation ? HttpMethods.Put : HttpMethods.Get;
        http.Request.Path = (browser ? Browser : Api) + (mutation ? Separator + Target : string.Empty);
        if (mutation)
        {
            http.Request.ContentType = JsonType;
            http.Request.Headers[OriginHeader] = Origin;
            http.Request.Headers[Csrf] = Proof;
            http.Request.Headers.IfMatch = CatalogueVersion.Create(CatalogueVersion.Audience, Target, InitialRevision);
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Input));
        }
        await using var output = new MemoryStream();
        http.Response.Body = output;
        change(http);
        if (browser)
        {
            await CatalogueProxy.HandleAsync(http, CatalogueVersion.Audience, mutation ? CatalogueProxyAction.Edit : CatalogueProxyAction.List, mutation ? Target : null);
        }
        else if (mutation)
        {
            await CatalogueMutationEndpoint.HandleAsync(http, CatalogueResource.Audience, Target);
        }
        else
        {
            await CatalogueReadEndpoint.HandleAsync(http, CatalogueResource.Audience);
        }
        Assert.Equal(expected, http.Response.StatusCode);
        Assert.Equal(NoStore, http.Response.Headers.CacheControl);
        Assert.Equal(NoSniff, http.Response.Headers.XContentTypeOptions);
        Assert.Equal(0, http.Response.Headers.ETag.Count);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        using var body = JsonDocument.Parse(output.ToArray());
        Assert.Equal(expected, body.RootElement.GetProperty(StatusField).GetInt32());
    }
}
