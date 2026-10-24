using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Bounds product callback framing without substituting for native protocol validation.</summary>
public sealed class BrowserCallbackRequestTests
{
    private const string SyntheticBearer = "Bearer synthetic";

    /// <summary>Valid code and error response framing remains native-owned; unsupported transport or ambiguous fields fail before processing.</summary>
    [Theory]
    [InlineData("GET", "https", "?state=opaque&code=opaque&iss=issuer", null, true)]
    [InlineData("GET", "https", "?state=opaque&error=access_denied", null, true)]
    [InlineData("POST", "https", "?state=opaque&code=opaque", null, false)]
    [InlineData("GET", "http", "?state=opaque&code=opaque", null, false)]
    [InlineData("GET", "https", "?state=one&state=two&code=opaque", null, false)]
    [InlineData("GET", "https", "?State=opaque&code=opaque", null, false)]
    [InlineData("GET", "https", "?state=opaque&returnUrl=unexpected", null, false)]
    [InlineData("GET", "https", "?state=&code=opaque", null, false)]
    [InlineData("GET", "https", "?state=opaque&code=opaque", "application/x-www-form-urlencoded", false)]
    public void Given_CallbackFraming_When_AdmissionRuns_Then_OnlyBoundedNativeQueryShapesProceed(string method, string scheme, string query, string? contentType, bool expected)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Scheme = scheme;
        http.Request.QueryString = new(query);
        http.Request.ContentType = contentType;
        Assert.Equal(expected, BrowserCallbackRequest.Valid(http.Request));
        http.Request.Headers.Authorization = SyntheticBearer;
        Assert.False(BrowserCallbackRequest.Valid(http.Request));
    }
}
