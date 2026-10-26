using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires exact issuer Origin at the real account framing guard; form parsing alone is not authenticated acceptance.</summary>
public sealed class BrowserAccountOriginTests
{
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const string RootPath = "/";
    private const string Origin = "https://issuer.example.test";
    private const string Client = "https://administrator.example.test/";
    private const string Field = "Intent";
    private const string Placeholder = "non-authenticating-placeholder";
    private const string Antiforgery = "__RequestVerificationToken";
    private const string Missing = "missing";
    private const string Multiple = "multiple";

    /// <summary>Native form policy never turns missing/null/foreign/multiple Origin into valid same-origin framing.</summary>
    [Theory]
    [InlineData(Origin)]
    [InlineData(Missing)]
    [InlineData("null")]
    [InlineData("https://foreign.example.test")]
    [InlineData(Multiple)]
    public async Task Given_NativeAccountForm_When_OriginChanges_Then_OnlyExactSingleOriginReachesParser(string origin)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.ContentType = FormMedia;
        http.Request.Headers.Origin = origin switch
        {
            Missing => StringValues.Empty,
            Multiple => new StringValues([Origin, Origin]),
            _ => new StringValues(origin)
        };
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { [Field] = Placeholder, [Antiforgery] = Placeholder });
        await using var body = new MemoryStream(await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        http.Request.Body = body;
        var form = await BrowserAccountRequest.FormAsync(http, new(Origin + RootPath, Client), Field);
        Assert.Equal(origin == Origin, form is not null);
        if (origin != Origin)
        {
            Assert.Equal(0, body.Position);
        }
    }
}
