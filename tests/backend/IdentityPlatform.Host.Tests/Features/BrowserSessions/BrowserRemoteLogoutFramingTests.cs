using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks exact logout-return wire framing before any protected proof or database operation.</summary>
public sealed class BrowserRemoteLogoutFramingTests
{
    private const string IssuerUri = "https://issuer.example.test/";
    private const string ClientUri = "https://administrator.example.test/";
    private const string PathFault = "path";
    private const string PathCaseFault = "path-case";
    private const string MethodFault = "method";
    private const string SchemeFault = "scheme";
    private const string HostFault = "host";
    private const string PortFault = "port";
    private const string BaseFault = "base";
    private const string BodyFault = "body";
    private const string LengthFault = "length";
    private const string TypeFault = "type";
    private const string AuthorizationFault = "authorization";
    private const string TransferFault = "transfer";
    private const string DuplicateFault = "duplicate";
    private const string ExtraFault = "extra";
    private const string AliasFault = "alias";
    private const string ReturnPath = "/bff/logout/return";
    private const string StateParameter = "state";
    private const string ForeignPath = "/other";
    private const string AliasedPath = "/BFF/LOGOUT/RETURN";
    private const string ForeignHost = "foreign.example.test";
    private const string ForeignBase = "/foreign";
    private const string JsonMedia = "application/json";
    private const string SyntheticBearer = "Bearer synthetic";
    private const string Chunked = "chunked";
    private const string StateQuery = "?state=";
    private const string DuplicateState = "&state=";
    private const string ExtraQuery = "&error=synthetic";
    private const string AliasedState = "?State=";
    private const string UnknownFault = "Unknown framing case.";
    private const int ForeignPort = 444;
    private static readonly BrowserSessionDestinations Destinations = new(IssuerUri, ClientUri);

    /// <summary>Each contradictory transport field denies independently; the original canonical empty HTTPS GET remains accepted.</summary>
    [Theory]
    [InlineData(PathFault)]
    [InlineData(PathCaseFault)]
    [InlineData(MethodFault)]
    [InlineData(SchemeFault)]
    [InlineData(HostFault)]
    [InlineData(PortFault)]
    [InlineData(BaseFault)]
    [InlineData(BodyFault)]
    [InlineData(LengthFault)]
    [InlineData(TypeFault)]
    [InlineData(AuthorizationFault)]
    [InlineData(TransferFault)]
    [InlineData(DuplicateFault)]
    [InlineData(ExtraFault)]
    [InlineData(AliasFault)]
    public async Task Given_CanonicalLogoutReturn_When_TransportIsSubstituted_Then_RequestDenies(string fault)
    {
        var http = new DefaultHttpContext();
        var state = BrowserLoginSelector.CreateSelector();
        http.Request.Path = ReturnPath;
        http.Request.Method = HttpMethods.Get;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(Destinations.Client.Host);
        http.Request.QueryString = new QueryString(QueryHelpers.AddQueryString(string.Empty, StateParameter, state));
        Assert.True(await BrowserLogoutRequest.ReturnAsync(http, Destinations) == state);
        Change(http, fault, state);
        Assert.Null(await BrowserLogoutRequest.ReturnAsync(http, Destinations));
    }

    /// <summary>Changes one actual ASP.NET transport property without invoking a replacement parser.</summary>
    private static void Change(HttpContext http, string fault, string state)
    {
        switch (fault)
        {
            case PathFault:
                http.Request.Path = ForeignPath;
                break;
            case PathCaseFault:
                http.Request.Path = AliasedPath;
                break;
            case MethodFault:
                http.Request.Method = HttpMethods.Post;
                break;
            case SchemeFault:
                http.Request.Scheme = Uri.UriSchemeHttp;
                break;
            case HostFault:
                http.Request.Host = new HostString(ForeignHost);
                break;
            case PortFault:
                http.Request.Host = new HostString(Destinations.Client.Host, ForeignPort);
                break;
            case BaseFault:
                http.Request.PathBase = ForeignBase;
                break;
            case BodyFault:
                http.Request.Body = new MemoryStream([1]);
                break;
            default:
                ChangeHeaders(http, fault, state);
                break;
        }
    }

    /// <summary>Changes only framing headers or query cardinality after route and body variants have been handled.</summary>
    private static void ChangeHeaders(HttpContext http, string fault, string state)
    {
        switch (fault)
        {
            case LengthFault:
                http.Request.ContentLength = 1;
                break;
            case TypeFault:
                http.Request.ContentType = JsonMedia;
                break;
            case AuthorizationFault:
                http.Request.Headers.Authorization = SyntheticBearer;
                break;
            case TransferFault:
                http.Request.Headers.TransferEncoding = Chunked;
                break;
            case DuplicateFault:
                http.Request.QueryString = new QueryString(StateQuery + state + DuplicateState + state);
                break;
            case ExtraFault:
                http.Request.QueryString = new QueryString(StateQuery + state + ExtraQuery);
                break;
            case AliasFault:
                http.Request.QueryString = new QueryString(AliasedState + state);
                break;
            default:
                throw new ArgumentException(UnknownFault);
        }
    }
}
