using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires exact status and safe error framing before any provider, cookie or native authentication service is resolved.</summary>
public sealed class BrowserStatusFramingTests
{
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string Authorization = "Authorization";
    private const string Query = "?error_description=untrusted";
    private const string Json = "application/json";
    private const string Post = "POST";
    private const string Head = "HEAD";
    private const string Http = "http";
    private const string Body = "body";

    /// <summary>Invalid requests terminate as noncacheable API errors without resolving absent storage or redirecting to HTML.</summary>
    [Theory]
    [InlineData(Authorization)]
    [InlineData(Query)]
    [InlineData(Json)]
    [InlineData(Post)]
    [InlineData(Head)]
    [InlineData(Http)]
    [InlineData(Body)]
    public async Task Given_NonStatusFraming_When_StatusBoundaryRuns_Then_BadRequestPrecedesStorageResolution(string fault)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = HttpMethods.Get;
        http.Request.Scheme = Uri.UriSchemeHttps;
        if (fault == Authorization)
        {
            http.Request.Headers.Authorization = Authorization;
        }
        if (fault == Query)
        {
            http.Request.QueryString = new QueryString(Query);
        }
        if (fault == Json)
        {
            http.Request.ContentType = Json;
        }
        if (fault is Post or Head)
        {
            http.Request.Method = fault;
        }
        if (fault == Http)
        {
            http.Request.Scheme = Http;
        }
        await using var body = new MemoryStream(fault == Body ? [1] : []);
        http.Request.Body = body;
        await BrowserSessionBoundary.RunAsync(http, services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal("no-referrer", http.Response.Headers[ReferrerPolicy]);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.Equal(0, http.Response.Headers.Location.Count);
    }

    /// <summary>The safe recovery view rejects untrusted query material and never reflects it or redirects automatically.</summary>
    [Fact]
    public async Task Given_ProtocolErrorQuery_When_RecoveryViewIsRequested_Then_InputIsRejectedWithoutReflection()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = HttpMethods.Get;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.QueryString = new QueryString(Query);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        await (await BrowserSessionError.ResultAsync(http)).ExecuteAsync(http);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal(0, output.Length);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }

    /// <summary>A valid error view supplies only an inert explicit navigation action, with no automatic challenge or secret-bearing inputs.</summary>
    [Fact]
    public async Task Given_QueryFreeErrorView_When_Rendered_Then_OnlyExplicitSafeRecoveryIsOffered()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = HttpMethods.Get;
        http.Request.Scheme = Uri.UriSchemeHttps;
        await using var output = new MemoryStream();
        http.Response.Body = output;
        await (await BrowserSessionError.ResultAsync(http)).ExecuteAsync(http);
        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        var html = System.Text.Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("href=\"/bff/login\"", html, StringComparison.Ordinal);
        IdentityPlatform.Testing.BrowserPresentationAssertions.ThemeOnly(html);
        Assert.DoesNotContain("<input", html, StringComparison.Ordinal);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }
}
