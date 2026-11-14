using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using IdentityPlatform.Sample.Api.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Exercises real HTTP orders admission with the production native validator and asymmetric JWT fixtures.</summary>
public sealed class SampleApiTests
{
    private const int RsaBits = 2048;
    private const string SyntheticKeyText = "synthetic-key";
    private const string OrdersField = "orders";
    private const string BearerScheme = "Bearer";
    private const string SyntheticOrder = "sample-order-001";
    private const string Http2BodyText = "http2-body";
    private const string QueryText = "query";
    private const string OrdersAccessTokenSyntheticText = "orders?access_token=synthetic";
    private const string NoTokenText = "no-token";
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private const string ValidText = "valid";
    private const string DuplicateText = "duplicate";
    private const string DuplicateBearer = "Bearer second";
    private const string DevelopmentEnvironment = "Development";
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string TrueValue = "true";
    private const string MalformedText = "malformed";
    private const string WrongIssuerText = "wrong-issuer";
    private const string HttpsWrongInvalidText = "https://wrong.invalid/";
    private const string IssuerAliasText = "issuer-alias";
    private const string WrongAudienceText = "wrong-audience";
    private const string AnotherApiText = "another-api";
    private const string OrdersApiText = "orders-api";
    private const string IdTokenText = "id-token";
    private const string JWTText = "JWT";
    private const string AtJwtText = "at+jwt";
    private const string WrongKeyText = "wrong-key";
    private const string SubjectClaim = "sub";
    private const string SyntheticUserText = "synthetic-user";
    private const string ScopeText = "scope";
    private const string NoScopeText = "no-scope";
    private const string OpenidText = "openid";
    private const string ScopePrefixText = "scope-prefix";
    private const string OrdersReadAllText = "orders.read.all";
    private const string OrdersReadText = "orders.read";
    private const int IssuedMinutesAgo = 10;
    private const string FutureText = "future";
    private const string NoExpiryText = "no-expiry";
    private const string ExpiredText = "expired";
    private const int TokenMinutes = 2;

    private const string Issuer = "https://issuer.dev.localhost:17443/";
    private const string Api = "https://orders.dev.localhost:17447/";

    /// <summary>Native signature/type/issuer/audience/lifetime checks precede exact scope authorization.</summary>
    [Theory]
    [InlineData("valid", 200)]
    [InlineData("wrong-issuer", 401)]
    [InlineData("issuer-alias", 401)]
    [InlineData("wrong-audience", 401)]
    [InlineData("wrong-key", 401)]
    [InlineData("id-token", 401)]
    [InlineData("expired", 401)]
    [InlineData("no-expiry", 401)]
    [InlineData("future", 401)]
    [InlineData("no-scope", 403)]
    [InlineData("scope-prefix", 403)]
    [InlineData("malformed", 401)]
    public async Task Given_SignedToken_When_OrdersAreRequested_Then_OnlyCompleteResourceAuthoritySucceeds(string scenario, int expected)
    {
        using var rsa = RSA.Create(RsaBits);
        var key = new RsaSecurityKey(rsa) { KeyId = SyntheticKeyText };
        await using var app = Create(key);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(Api);
        using var request = new HttpRequestMessage(HttpMethod.Get, OrdersField);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, Token(key, scenario));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("synthetic-user", body, StringComparison.Ordinal);
        Assert.Equal(expected == 200, body.Contains(SyntheticOrder, StringComparison.Ordinal));
    }

    /// <summary>Header-only admission rejects query, duplicate and HTTP/2 body transports even with an otherwise valid token.</summary>
    [Theory]
    [InlineData("query")]
    [InlineData("duplicate")]
    [InlineData("http2-body")]
    [InlineData("no-token")]
    public async Task Given_AmbiguousTransport_When_OrdersAreRequested_Then_RequestIsDenied(string scenario)
    {
        using var rsa = RSA.Create(RsaBits);
        var key = new RsaSecurityKey(rsa) { KeyId = SyntheticKeyText };
        await using var app = Create(key, scenario == Http2BodyText);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(Api);
        using var request = new HttpRequestMessage(HttpMethod.Get, scenario == QueryText ? OrdersAccessTokenSyntheticText : OrdersField);
        if (scenario != NoTokenText)
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeader, BearerPrefix + Token(key, ValidText));
        }
        if (scenario == DuplicateText)
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeader, DuplicateBearer);
        }
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Builds the exact API middleware with an explicit trusted public key, without network or persistence dependencies.</summary>
    private static WebApplication Create(SecurityKey key, bool body = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = DevelopmentEnvironment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [EnabledSetting] = TrueValue });
        builder.WebHost.UseTestServer();
        builder.Services.PostConfigure<OpenIddictValidationOptions>(options =>
        {
            options.Configuration = new OpenIddictConfiguration { Issuer = new Uri(Issuer) };
            options.Configuration.SigningKeys.Add(key);
        });
        if (body)
        {
            builder.Services.AddSingleton<IStartupFilter, BodyFilter>();
        }
        return SampleApplication.Create(builder);
    }

    /// <summary>Creates independently specified signed tokens, including missing-expiry and future-not-before counterexamples.</summary>
    private static string Token(SecurityKey key, string scenario)
    {
        if (scenario == MalformedText)
        {
            return MalformedText;
        }
        using var other = RSA.Create(RsaBits);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = scenario == WrongIssuerText ? HttpsWrongInvalidText : scenario == IssuerAliasText ? Issuer.TrimEnd('/') : Issuer,
            Audience = scenario == WrongAudienceText ? AnotherApiText : OrdersApiText,
            TokenType = scenario == IdTokenText ? JWTText : AtJwtText,
            SigningCredentials = new SigningCredentials(scenario == WrongKeyText ? new RsaSecurityKey(other) : key, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object> { [SubjectClaim] = SyntheticUserText, [ScopeText] = scenario == NoScopeText ? OpenidText : scenario == ScopePrefixText ? OrdersReadAllText : OrdersReadText },
            IssuedAt = DateTime.UtcNow.AddMinutes(-IssuedMinutesAgo),
            NotBefore = scenario == FutureText ? DateTime.UtcNow.AddMinutes(1) : DateTime.UtcNow.AddMinutes(-IssuedMinutesAgo),
            Expires = scenario == NoExpiryText ? null : scenario == ExpiredText ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(TokenMinutes)
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>Represents the server-provided HTTP/2 body capability independently of optional framing headers.</summary>
    private sealed class BodyFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    /// <summary>Injects the real server feature before the ordinary application pipeline, leaving token validation unchanged.</summary>
    private sealed class BodyFilter : IStartupFilter
    {
        /// <summary>Attaches body detection exactly as an HTTP/2 server does when no Content-Length is present.</summary>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) => { context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyFeature()); return following(context); });
            next(app);
        };
    }
}
