using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises the actual logout adapter's invalid framing before any database, proof or antiforgery collaborator is used.</summary>
public sealed class BrowserLogoutFramingTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://administrator.example.test";
    private const string ClientUri = "https://administrator.example.test/";
    private const string Method = "method";
    private const string Insecure = "insecure";
    private const string Query = "query";
    private const string GetContentType = "get-type";
    private const string Body = "body";
    private const string PostType = "post-type";
    private const string Overflow = "overflow";
    private const string Authorization = "authorization";
    private const string QueryValue = "?untrusted=value";
    private const string Json = "application/json";
    private const string Form = "application/x-www-form-urlencoded";
    private const string Origin = "Origin";
    private const string NoReferrer = "Referrer-Policy";
    private const int OverflowBytes = 8193;

    /// <summary>Null unreachable collaborators make any accidental admission fail; only framing rejection can produce the required finite 400.</summary>
    [Theory]
    [InlineData(Method)]
    [InlineData(Insecure)]
    [InlineData(Query)]
    [InlineData(GetContentType)]
    [InlineData(Body)]
    [InlineData(PostType)]
    [InlineData(Overflow)]
    [InlineData(Authorization)]
    public async Task Given_InvalidLogoutFraming_When_ActualBoundaryRuns_Then_NoStorageOrCookieWorkCanOccur(string fault)
    {
        await using var services = new ServiceCollection().AddLogging().AddScoped(_ => new BrowserLogoutHandler(null!, TimeProvider.System,
            null!, null!, null!, new BrowserSessionDestinations(Issuer, ClientUri), null!)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Path = BrowserLogoutPage.Path;
        http.Request.Scheme = fault == Insecure ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;
        http.Request.Method = fault == Method ? HttpMethods.Head : fault is PostType or Overflow ? HttpMethods.Post : HttpMethods.Get;
        http.Request.ContentType = fault is GetContentType or PostType ? Json : fault == Overflow ? Form : null;
        http.Request.Headers[Origin] = Client;
        if (fault == Query)
        {
            http.Request.QueryString = new QueryString(QueryValue);
        }
        if (fault == Authorization)
        {
            http.Request.Headers.Authorization = Authorization;
        }
        await using var input = new MemoryStream(fault == Overflow ? new byte[OverflowBytes] : fault == Body ? [1] : []);
        await using var output = new MemoryStream();
        http.Request.Body = input;
        http.Response.Body = output;
        await BrowserLoginBoundary.RunAsync(http, services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal("no-referrer", http.Response.Headers[NoReferrer]);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.Equal(0, http.Response.Headers.Location.Count);
        Assert.Equal(0, output.Length);
    }
}
