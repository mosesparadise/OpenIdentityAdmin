using System.Text;
using IdentityPlatform.AuthServer.Features.Protocol.UserInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises exact UserInfo framing before native extraction, including HTTP/2 bodies without length or transfer headers.</summary>
public sealed class UserInfoTransportTests
{
    private const string Cleartext = "http";
    private const string Secure = "https";
    private const string Alias = "/connect/%75serinfo";
    private const string DuplicateForm = "access_token=first&access_token=second";
    private const string ExtraForm = "client_id=other";
    private const string TokenForm = "access_token=synthetic.token";
    private const string TokenQuery = "?access_token=synthetic";
    private const string HeaderSeparator = ",";
    private const string BearerPrefix = "Bearer ";
    private const int OversizedLength = 16_385;

    private const string Path = "/connect/userinfo";
    private const string FormType = "application/x-www-form-urlencoded";
    private const string Header = "Bearer synthetic.token";

    /// <summary>Only one canonical secure bearer transport is admitted; no query, duplicate, alias or body normalization widens it.</summary>
    [Theory]
    [InlineData(UserInfoFraming.GetHeader, true)]
    [InlineData(UserInfoFraming.PostHeader, true)]
    [InlineData(UserInfoFraming.PostForm, true)]
    [InlineData(UserInfoFraming.Missing, true)]
    [InlineData(UserInfoFraming.Query, false)]
    [InlineData(UserInfoFraming.DuplicateHeader, false)]
    [InlineData(UserInfoFraming.DuplicateForm, false)]
    [InlineData(UserInfoFraming.Mixed, false)]
    [InlineData(UserInfoFraming.ExtraForm, false)]
    [InlineData(UserInfoFraming.Cleartext, false)]
    [InlineData(UserInfoFraming.RawAlias, false)]
    [InlineData(UserInfoFraming.GetBody, false)]
    [InlineData(UserInfoFraming.Http2Body, false)]
    [InlineData(UserInfoFraming.CombinedHeader, false)]
    [InlineData(UserInfoFraming.OversizedHeader, false)]
    public async Task Given_UserInfoRequest_When_RawFramingIsChecked_Then_OnlyOneCanonicalTransportReachesNativeValidation(UserInfoFraming defect, bool accepted)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = defect == UserInfoFraming.Cleartext ? Cleartext : Secure;
        http.Request.Path = Path;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = defect == UserInfoFraming.RawAlias ? Alias : Path;
        var post = defect is UserInfoFraming.PostHeader or UserInfoFraming.PostForm or UserInfoFraming.DuplicateForm or UserInfoFraming.Mixed or UserInfoFraming.ExtraForm;
        http.Request.Method = post ? HttpMethods.Post : HttpMethods.Get;
        http.Request.Headers.Authorization = defect is UserInfoFraming.PostForm or UserInfoFraming.DuplicateForm or UserInfoFraming.ExtraForm or UserInfoFraming.Missing ? [] : Header;
        Apply(http, defect);
        if (post)
        {
            var body = defect switch
            {
                UserInfoFraming.PostHeader => string.Empty,
                UserInfoFraming.DuplicateForm => DuplicateForm,
                UserInfoFraming.ExtraForm => ExtraForm,
                _ => TokenForm
            };
            http.Request.ContentType = FormType;
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            http.Request.ContentLength = http.Request.Body.Length;
        }
        var transaction = new OpenIddictServerTransaction { EndpointType = OpenIddictServerEndpointType.UserInfo };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        var context = new OpenIddictServerEvents.ExtractUserInfoRequestContext(transaction);
        await UserInfoTransport.ValidateAsync(context);
        Assert.Equal(!accepted, context.IsRejected);
    }

    /// <summary>Changes one transport fact independently of native query/form parsing.</summary>
    private static void Apply(DefaultHttpContext http, UserInfoFraming defect)
    {
        switch (defect)
        {
            case UserInfoFraming.Query:
                http.Request.QueryString = new(TokenQuery);
                break;
            case UserInfoFraming.DuplicateHeader:
                http.Request.Headers.Authorization = new([Header, Header]);
                break;
            case UserInfoFraming.CombinedHeader:
                http.Request.Headers.Authorization = Header + HeaderSeparator + Header;
                break;
            case UserInfoFraming.OversizedHeader:
                http.Request.Headers.Authorization = BearerPrefix + new string('a', OversizedLength);
                break;
            case UserInfoFraming.GetBody:
                http.Request.ContentLength = 1;
                break;
            case UserInfoFraming.Http2Body:
                http.Features.Set<IHttpRequestBodyDetectionFeature>(new Body());
                break;
        }
    }

    /// <summary>Models the actual server body's framing signal when HTTP/2 omits both legacy body headers.</summary>
    private sealed class Body : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}

/// <summary>Names independent transport controls while leaving missing credentials for native bearer validation.</summary>
public enum UserInfoFraming
{
    GetHeader, PostHeader, PostForm, Missing, Query, DuplicateHeader, DuplicateForm, Mixed, ExtraForm, Cleartext, RawAlias, GetBody, Http2Body, CombinedHeader, OversizedHeader
}
