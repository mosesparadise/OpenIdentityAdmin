using IdentityPlatform.Admin.Bff.Features.AdminUi;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.AdminUi;

/// <summary>Requires exact embedded workspace ownership, restrictive response headers and explicit Development-only composition.</summary>
public sealed class AdministratorUiTests
{
    private const string SessionPath = "/session";
    private const string EncodedFault = "encoded";
    private const string EncodedPath = "/%73ession";

    private const string ReferrerHeader = "Referrer-Policy";
    private const string MethodFault = "method";
    private const string HttpFault = "http";
    private const string QueryFault = "query";
    private const string TypeFault = "type";
    private const string LengthFault = "length";
    private const string TransferFault = "transfer";
    private const string UnknownBodyFault = "http2-body";
    private const string QueryValue = "?returnUrl=https://foreign.example";
    private const string JsonType = "application/json";
    private const string Chunked = "chunked";
    private const string RootPath = "/";
    private const string StatusPath = "/bff/session";

    /// <summary>Every reviewed resource serves real embedded bytes with its independent media type and no credential mutation.</summary>
    [Theory]
    [InlineData(RootPath, "text/html; charset=utf-8")]
    [InlineData("/session", "text/html; charset=utf-8")]
    [InlineData("/admin-assets/app.js", "text/javascript; charset=utf-8")]
    [InlineData("/admin-assets/app.css", "text/css; charset=utf-8")]
    [InlineData("/admin-assets/appearance.js", "text/javascript; charset=utf-8")]
    [InlineData("/admin-assets/notices.txt", "text/plain; charset=utf-8")]
    [InlineData("/admin-assets/app.js.LEGAL.txt", "text/plain; charset=utf-8")]
    public async Task Given_ExactWorkspaceResource_When_GetOrHeadRuns_Then_EmbeddedBytesAndRestrictiveHeadersAreServed(string path, string type)
    {
        Assert.True(AdministratorUiAssets.TryGet(path, out var asset));
        foreach (var method in new[] { HttpMethods.Get, HttpMethods.Head })
        {
            var http = Context(method);
            await using var output = new MemoryStream();
            http.Response.Body = output;
            await AdministratorUiAssets.SendAsync(http, asset);
            Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
            Assert.Equal(type, http.Response.ContentType);
            if (method == HttpMethods.Get)
            {
                Assert.Equal(asset.Bytes, output.ToArray());
            }
            Assert.True(http.Response.ContentLength > 0);
            Assert.Equal(method == HttpMethods.Get ? http.Response.ContentLength : 0, output.Length);
            Assert.Equal("no-store", http.Response.Headers.CacheControl);
            Assert.Equal("nosniff", http.Response.Headers.XContentTypeOptions);
            Assert.Equal("DENY", http.Response.Headers.XFrameOptions);
            Assert.Equal("no-referrer", http.Response.Headers[ReferrerHeader]);
            Assert.Equal("default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'; object-src 'none'", http.Response.Headers.ContentSecurityPolicy);
            Assert.Equal(0, http.Response.Headers.SetCookie.Count);
            Assert.Equal(0, http.Response.Headers.Location.Count);
        }
    }

    /// <summary>Unknown or aliased documents cannot turn protocol and management paths into a successful SPA fallback.</summary>
    [Theory]
    [InlineData("/Session")]
    [InlineData("/session/")]
    [InlineData(EncodedPath)]
    [InlineData("/admin-assets/../index.html")]
    [InlineData("/admin-assets/missing.js")]
    [InlineData(StatusPath)]
    [InlineData("/bff/login")]
    [InlineData("/api/applications")]
    public void Given_UnownedPath_When_AssetsAreMatched_Then_NoResourceIsSelected(string path) =>
        Assert.False(AdministratorUiAssets.TryGet(new PathString(path), out _));

    /// <summary>Method, TLS and framed or headerless HTTP/2 body substitution fail before any document is published.</summary>
    [Theory]
    [InlineData(MethodFault, 405)]
    [InlineData(HttpFault, 400)]
    [InlineData(QueryFault, 400)]
    [InlineData(TypeFault, 400)]
    [InlineData(LengthFault, 400)]
    [InlineData(TransferFault, 400)]
    [InlineData(UnknownBodyFault, 400)]
    [InlineData(EncodedFault, 400)]
    public async Task Given_InvalidResourceFraming_When_Served_Then_NoBytesOrCredentialsArePublished(string fault, int status)
    {
        var http = Context(fault == MethodFault ? HttpMethods.Post : HttpMethods.Get);
        ApplyFault(http, fault);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        Assert.True(AdministratorUiAssets.TryGet(RootPath, out var asset));
        await AdministratorUiAssets.SendAsync(http, asset);
        Assert.Equal(status, http.Response.StatusCode);
        Assert.Equal(0, output.Length);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        if (status == StatusCodes.Status405MethodNotAllowed)
        {
            Assert.Equal("GET, HEAD", http.Response.Headers.Allow);
        }
    }

    /// <summary>Production or missing explicit registration cannot install workspace routing.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Given_UnapprovedComposition_When_WorkspaceIsMounted_Then_StartupFails(bool development, bool registered)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = development ? Environments.Development : Environments.Production
        });
        if (registered)
        {
            builder.Services.AddAdministratorUi();
        }
        using var host = builder.Build();
        Assert.Throws<InvalidOperationException>(() => new ApplicationBuilder(host.Services).UseAdministratorUi());
    }

    /// <summary>The opt-in owns only exact workspace paths and a fixed return link; default login navigation is preserved.</summary>
    [Fact]
    public async Task Given_ExplicitDevelopmentComposition_When_WorkspaceOrProtocolIsRequested_Then_OnlyWorkspaceIsOwned()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Services.AddAdministratorUi();
        using var host = builder.Build();
        var app = new ApplicationBuilder(host.Services);
        app.UseAdministratorUi();
        app.Run(http => { http.Response.StatusCode = StatusCodes.Status418ImATeapot; return Task.CompletedTask; });
        foreach (var path in new[] { RootPath, StatusPath })
        {
            var http = Context(HttpMethods.Get);
            http.RequestServices = host.Services;
            http.Request.Path = path;
            await using var output = new MemoryStream();
            http.Response.Body = output;
            await app.Build()(http);
            Assert.Equal(path == RootPath ? StatusCodes.Status200OK : StatusCodes.Status418ImATeapot, http.Response.StatusCode);
            Assert.Equal(RootPath, BrowserWorkspaceNavigation.Destination(http));
            Assert.Contains("href=\"/\"", BrowserWorkspaceNavigation.Link(http), StringComparison.Ordinal);
        }
        using var ordinary = new ServiceCollection().BuildServiceProvider();
        var original = new DefaultHttpContext { RequestServices = ordinary };
        Assert.Equal("/bff/login", BrowserWorkspaceNavigation.Destination(original));
        Assert.Empty(BrowserWorkspaceNavigation.Link(original));
    }

    /// <summary>Applies one independently selected framing defect before calling the production resource boundary.</summary>
    private static void ApplyFault(DefaultHttpContext http, string fault)
    {
        switch (fault)
        {
            case HttpFault:
                http.Request.Scheme = Uri.UriSchemeHttp;
                break;
            case QueryFault:
                http.Request.QueryString = new(QueryValue);
                break;
            case TypeFault:
                http.Request.ContentType = JsonType;
                break;
            case LengthFault:
                http.Request.ContentLength = 1;
                break;
            case TransferFault:
                http.Request.Headers.TransferEncoding = Chunked;
                break;
            case EncodedFault:
                http.Request.Path = new PathString(SessionPath);
                http.Features.Get<IHttpRequestFeature>()!.RawTarget = EncodedPath;
                break;
            case UnknownBodyFault:
                http.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection());
                break;
        }
    }

    /// <summary>Creates a bodyless HTTPS request without storage or authentication services.</summary>
    private static DefaultHttpContext Context(string method)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = RootPath;
        http.Request.Scheme = Uri.UriSchemeHttps;
        return http;
    }

    /// <summary>Represents a legitimate HTTP/2 DATA-capable request with no content length or transfer-encoding header.</summary>
    private sealed class BodyDetection : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
