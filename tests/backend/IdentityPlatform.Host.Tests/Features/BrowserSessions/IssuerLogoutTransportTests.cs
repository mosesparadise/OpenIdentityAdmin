using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires actual bounded HTTP bytes and rejects ambiguous transport before native extraction can normalize input.</summary>
public sealed class IssuerLogoutTransportTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Path = "/connect/end-session";
    private const string Selector = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Media = "application/x-www-form-urlencoded";
    private const string Foreign = "https://foreign.example.test";
    private const string Chunked = "chunked";
    private const string DuplicateCookie = "__Host-identity-administrator=first; __Host-identity-administrator=second";
    private const string Cookie = "__Host-identity-administrator=first";
    private const string ConfirmationField = "confirmation";
    private const string Antiforgery = "__RequestVerificationToken";
    private const int MaximumBytes = 16384;

    /// <summary>Exercises body mismatch, pre-parsed input, origin, cookies and unsupported transports with unchanged valid-form positive framing.</summary>
    [Theory]
    [InlineData(IssuerLogoutTransportFault.None)]
    [InlineData(IssuerLogoutTransportFault.ShortDeclared)]
    [InlineData(IssuerLogoutTransportFault.LongDeclared)]
    [InlineData(IssuerLogoutTransportFault.Overflow)]
    [InlineData(IssuerLogoutTransportFault.TransferEncoding)]
    [InlineData(IssuerLogoutTransportFault.Preparsed)]
    [InlineData(IssuerLogoutTransportFault.Origin)]
    [InlineData(IssuerLogoutTransportFault.MissingOrigin)]
    [InlineData(IssuerLogoutTransportFault.Cookie)]
    [InlineData(IssuerLogoutTransportFault.CookieLines)]
    [InlineData(IssuerLogoutTransportFault.Insecure)]
    public async Task Given_ExactBrowserForm_When_TransportIsChanged_Then_OnlyUnambiguousBoundedBytesAreAccepted(IssuerLogoutTransportFault fault)
    {
        using var content = new FormUrlEncodedContent(Fields());
        var bytes = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var http = Context();
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentType = Media;
        http.Request.ContentLength = bytes.Length;
        http.Request.Headers.Origin = new Uri(Issuer).GetLeftPart(UriPartial.Authority);
        await using var body = new MemoryStream(fault == IssuerLogoutTransportFault.Overflow ? new byte[MaximumBytes + 1] : bytes);
        http.Request.Body = body;
        Mutate(http, fault);
        Assert.Equal(fault == IssuerLogoutTransportFault.None, await IssuerLogoutInput.AllowsAsync(http, new Uri(Issuer), TestContext.Current.CancellationToken));
    }

    /// <summary>Unknown-length GET bodies cannot pass a declared empty-body check.</summary>
    [Fact]
    public async Task Given_ValidGetQuery_When_ActualBodyContainsBytes_Then_ConfirmationFramingDenies()
    {
        var http = Context();
        http.Request.Method = HttpMethods.Get;
        http.Request.QueryString = QueryString.Create(Fields().Where(pair => pair.Key != Antiforgery && pair.Key != ConfirmationField)
            .Select(pair => KeyValuePair.Create<string, string?>(pair.Key, pair.Value)));
        await using var body = new MemoryStream([1]);
        http.Request.Body = body;
        Assert.False(await IssuerLogoutInput.AllowsAsync(http, new Uri(Issuer), TestContext.Current.CancellationToken));
        body.Position = body.Length;
        Assert.True(await IssuerLogoutInput.AllowsAsync(http, new Uri(Issuer), TestContext.Current.CancellationToken));
    }

    /// <summary>Changes one independent transport property without fabricating an authenticated session.</summary>
    private static void Mutate(HttpContext http, IssuerLogoutTransportFault fault)
    {
        switch (fault)
        {
            case IssuerLogoutTransportFault.ShortDeclared:
                http.Request.ContentLength--;
                break;
            case IssuerLogoutTransportFault.LongDeclared:
                http.Request.ContentLength++;
                break;
            case IssuerLogoutTransportFault.TransferEncoding:
                http.Request.Headers.TransferEncoding = Chunked;
                break;
            case IssuerLogoutTransportFault.Preparsed:
                http.Features.Set<IFormFeature>(new FormFeature(new FormCollection([])));
                break;
            case IssuerLogoutTransportFault.Origin:
                http.Request.Headers.Origin = Foreign;
                break;
            case IssuerLogoutTransportFault.MissingOrigin:
                http.Request.Headers.Origin = Microsoft.Extensions.Primitives.StringValues.Empty;
                break;
            case IssuerLogoutTransportFault.Cookie:
                http.Request.Headers.Cookie = DuplicateCookie;
                break;
            case IssuerLogoutTransportFault.CookieLines:
                http.Request.Headers.Cookie = new Microsoft.Extensions.Primitives.StringValues([Cookie, Cookie]);
                break;
            case IssuerLogoutTransportFault.Insecure:
                http.Request.Scheme = Uri.UriSchemeHttp;
                break;
        }
    }

    /// <summary>Creates the fixed HTTPS transport without installing fake authentication or native validation.</summary>
    private static DefaultHttpContext Context()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = HostString.FromUriComponent(new Uri(Issuer));
        http.Request.Path = Path;
        return http;
    }

    /// <summary>Supplies independent literal protocol expectations, including separately named nonauthenticating form placeholders.</summary>
    private static Dictionary<string, string> Fields()
    {
        const string Client = "client_id", ClientValue = "identity-admin-bff", Return = "post_logout_redirect_uri", ReturnValue = "https://administrator.example.test/bff/logout/return";
        const string Hint = "logout_hint", State = "state", Confirmation = "confirmation", Placeholder = "opaque-test-placeholder";
        return new()
        {
            [Client] = ClientValue,
            [Return] = ReturnValue,
            [Hint] = Selector,
            [State] = Selector,
            [Antiforgery] = Placeholder,
            [Confirmation] = Placeholder
        };
    }
}

/// <summary>Names independent framing defects for finite pure transport controls.</summary>
public enum IssuerLogoutTransportFault
{
    None, ShortDeclared, LongDeclared, Overflow, TransferEncoding, Preparsed, Origin, MissingOrigin, Cookie, CookieLines, Insecure
}
