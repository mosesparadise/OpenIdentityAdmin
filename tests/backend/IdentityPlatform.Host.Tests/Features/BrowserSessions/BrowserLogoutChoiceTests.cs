using System.Text;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Proves the new optional logout choice is closed and cannot widen the protected form into destination or authority input.</summary>
public sealed class BrowserLogoutChoiceTests
{
    private const string Origin = "https://administrator.example.test";
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://administrator.example.test/";
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const string OriginHeader = "Origin";
    private const string NativeField = "__RequestVerificationToken=synthetic";

    /// <summary>Unreachable persistence collaborators make accidental admission fail before any test could claim a safe rejection.</summary>
    [Theory]
    [InlineData("&Mode=foreign")]
    [InlineData("&Mode=")]
    [InlineData("&Mode=issuer&Mode=local")]
    [InlineData("&return_uri=https%3A%2F%2Fforeign.example.test")]
    public async Task Given_UnsupportedLogoutChoice_When_ActualHandlerParsesForm_Then_NoAuthorityOrPersistenceIsConsulted(string fields)
    {
        var http = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(NativeField + fields);
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.ContentType = FormMedia;
        http.Request.ContentLength = bytes.Length;
        http.Request.Headers[OriginHeader] = Origin;
        await using var body = new MemoryStream(bytes);
        http.Request.Body = body;
        var handler = new BrowserLogoutHandler(null!, TimeProvider.System, null!, null!, null!, new(Issuer, Client), null!);
        var response = await handler.RunAsync(http);
        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)response).StatusCode);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }
}
