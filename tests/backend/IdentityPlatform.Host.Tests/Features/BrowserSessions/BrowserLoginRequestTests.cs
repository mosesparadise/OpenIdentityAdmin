using System.Text;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises actual raw cookie and streamed form framing before native antiforgery can consume normalized input.</summary>
public sealed class BrowserLoginRequestTests
{
    private const string OverflowFault = "overflow";
    private const string DuplicateFault = "duplicate";
    private const string ExtraFault = "extra";
    private const string LengthFault = "length";
    private const string ParsedFault = "parsed";
    private const string OriginFault = "origin";
    private const string FirstField = "=a&";
    private const string SecondValue = "=b";
    private const string ExtraField = "=a&Generation=0";
    private const string FieldValue = "=a";
    private const string ForeignOrigin = "https://other.example.test";
    private const string Issuer = "https://issuer.example.test/";
    private const string RootPath = "/";
    private const string EncodedToken = "=native%2Btoken";
    private const string DecodedToken = "native+token";
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const int OversizedBody = 8193;
    private const string Origin = "https://administrator.example.test";
    private const string Field = "__RequestVerificationToken";
    private const string CookieName = "__Host-identity-admin-generation";

    /// <summary>Raw duplicate, alias, empty and quoted cookies cannot be hidden by the parsed cookie dictionary.</summary>
    [Theory]
    [InlineData("__Host-identity-admin-generation=a; __Host-identity-admin-generation=b")]
    [InlineData("__host-identity-admin-generation=a")]
    [InlineData("__Host-identity-admin-generation=")]
    [InlineData("__Host-identity-admin-generation=\"a\"")]
    public void Given_AmbiguousRawCookie_When_ProofIsSelected_Then_RequestDenies(string cookie)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = cookie;
        Assert.False(BrowserLoginRequest.Cookie(http.Request, CookieName, out _));
    }

    /// <summary>Actual overflow, duplicate/extra decoded fields and pre-parsed form features cannot bypass the bounded native parser.</summary>
    [Theory]
    [InlineData(OverflowFault)]
    [InlineData(DuplicateFault)]
    [InlineData(ExtraFault)]
    [InlineData(LengthFault)]
    [InlineData(ParsedFault)]
    [InlineData(OriginFault)]
    public async Task Given_HostileFormTransport_When_ActualBytesAreParsed_Then_NoFormIsAdmitted(string fault)
    {
        var http = Request();
        var body = fault switch
        {
            OverflowFault => new string('x', OversizedBody),
            DuplicateFault => Field + FirstField + Field + SecondValue,
            ExtraFault => Field + ExtraField,
            _ => Field + FieldValue
        };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (fault == LengthFault)
        {
            http.Request.ContentLength = body.Length - 1;
        }
        if (fault == ParsedFault)
        {
            http.Features.Set<IFormFeature>(new FormFeature(new FormCollection(new())));
        }
        if (fault == OriginFault)
        {
            http.Request.Headers.Origin = ForeignOrigin;
        }
        Assert.False(await BrowserLoginRequest.FormAsync(http, new(Issuer, Origin + RootPath)));
    }

    /// <summary>The exact field is parsed natively only after its actual bytes fit, and a body on a declared-empty GET is detected.</summary>
    [Fact]
    public async Task Given_ExactBoundedForm_When_Parsed_Then_NativeAntiforgeryReceivesOnlyThatForm()
    {
        var http = Request();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Field + EncodedToken));
        Assert.True(await BrowserLoginRequest.FormAsync(http, new(Issuer, Origin + RootPath)));
        Assert.True(http.Request.Form[Field] == DecodedToken);
        http.Request.Method = HttpMethods.Get;
        http.Request.Body = new MemoryStream([1]);
        Assert.False(await BrowserLoginRequest.EmptyAsync(http));
    }

    /// <summary>Malformed actual UTF-8 is rejected before native decoding, even with otherwise valid form headers.</summary>
    [Fact]
    public async Task Given_InvalidUtf8Body_When_ProtectedFormIsParsed_Then_InputDenies()
    {
        const byte invalidUtf8 = 255;
        var http = Request();
        http.Request.Body = new MemoryStream([invalidUtf8]);
        Assert.False(await BrowserLoginRequest.FormAsync(http, new(Issuer, Origin + RootPath)));
    }

    /// <summary>Creates a real HTTP request with only the reviewed origin/media, leaving body length deliberately undeclared.</summary>
    private static DefaultHttpContext Request()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.ContentType = FormMedia;
        http.Request.Headers.Origin = Origin;
        return http;
    }
}
