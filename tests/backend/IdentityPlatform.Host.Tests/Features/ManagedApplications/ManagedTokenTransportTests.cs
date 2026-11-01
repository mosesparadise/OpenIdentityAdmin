using System.Text;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Tests raw backchannel framing before native Basic, secret and PKCE authentication are permitted to run.</summary>
public sealed class ManagedTokenTransportTests
{
    private const string TokenPath = "/connect/token";
    private const string Form = "application/x-www-form-urlencoded";
    private const string Multipart = "multipart/form-data; boundary=test-boundary";
    private const string Fields = "grant_type=authorization_code&code=synthetic&redirect_uri=https%3A%2F%2Frp.example.test%2Fcallback&code_verifier=synthetic";
    private const string Extra = "&client_secret=synthetic";
    private const string Duplicate = "&code=second";
    private const string Basic = "Basic c3ludGhldGljOnN5bnRoZXRpYw==";
    private const string Https = "https";
    private const string Http = "http";
    private const string AliasPath = "/connect/%74oken";

    /// <summary>Rejects multipart, extra/duplicate form fields, raw path aliases and cleartext even when form parsing could normalize them.</summary>
    [Theory]
    [InlineData(ManagedTokenFraming.Valid)]
    [InlineData(ManagedTokenFraming.Multipart)]
    [InlineData(ManagedTokenFraming.Extra)]
    [InlineData(ManagedTokenFraming.Duplicate)]
    [InlineData(ManagedTokenFraming.RawPath)]
    [InlineData(ManagedTokenFraming.Cleartext)]
    public async Task Given_ManagedTokenWireRequest_When_FramingIsChecked_Then_OnlyExactSecureCodeFormIsAccepted(ManagedTokenFraming defect)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = defect == ManagedTokenFraming.Cleartext ? Http : Https;
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = TokenPath;
        http.Request.ContentType = defect == ManagedTokenFraming.Multipart ? Multipart : Form;
        http.Request.Headers.Authorization = Basic;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = defect == ManagedTokenFraming.RawPath ? AliasPath : TokenPath;
        var body = Fields + (defect == ManagedTokenFraming.Extra ? Extra : defect == ManagedTokenFraming.Duplicate ? Duplicate : string.Empty);
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.ContentLength = http.Request.Body.Length;
        var transaction = new OpenIddictServerTransaction { EndpointType = OpenIddictServerEndpointType.Token };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        var context = new OpenIddictServerEvents.ProcessRequestContext(transaction);
        Assert.True(ManagedTokenTransport.Candidate(context));
        await ManagedTokenTransport.ValidateAsync(context);
        Assert.Equal(defect != ManagedTokenFraming.Valid, context.IsRejected);
    }
}

/// <summary>Names independent exact form and transport ambiguities.</summary>
public enum ManagedTokenFraming
{
    Valid, Multipart, Extra, Duplicate, RawPath, Cleartext
}
