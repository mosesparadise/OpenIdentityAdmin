using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises the registered browser origin through a real credential-free HTTPS exchange and user-bound UserInfo.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserClientJourneyTests(BootstrapFixture fixture)
{
    private const string SecondClient = "second-browser";
    private const string Display = "Browser portal";
    private const string Profile = "managed-browser-v1";
    private const string Token = "access_token";
    private const string OriginHeader = "Origin";
    private const string RequestedMethod = "Access-Control-Request-Method";
    private const string RequestedHeaders = "Access-Control-Request-Headers";
    private const string Get = "GET";
    private const string AuthorizationHeader = "authorization";
    private const string Discovery = "/.well-known/openid-configuration";
    private const string JwksUri = "jwks_uri";
    private const string Keys = "keys";
    private const string Bearer = "Bearer";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";

    private const string Origin = "https://portal.example.test";
    private const string Other = "https://second.example.test";
    private const string Callback = "https://signin.example.test/callback";
    private const string CallbackOrigin = "https://signin.example.test";
    private const string Client = "managed-browser-portal";
    private const string UserInfo = "/connect/userinfo";
    private const string AllowOrigin = "Access-Control-Allow-Origin";

    /// <summary>Uses a second registered origin to distinguish shared preflight admission from per-client token and UserInfo authority.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SeparateCallbacksAndOrigins_When_RedeemingAndReadingUserInfo_Then_OnlyTheAuthorizedBrowserCanRead(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var test = await RegisterAsync(http, Client, Origin);
        await RegisterAsync(http, SecondClient, Other);
        using var portal = Browser(http, Origin);
        using var other = Browser(http, Other);
        using var callback = Browser(http, CallbackOrigin);
        await PreflightAsync(portal, allowed: true);
        await PreflightAsync(other, allowed: true);
        await PreflightAsync(callback, allowed: false);
        await DiscoveryAsync(portal);
        var subject = await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.Address, verified: true);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = await flow.CodeAsync();
        using var wrong = await flow.ExchangeAsync(code, client: other);
        Denied(wrong);
        using var response = await flow.ExchangeAsync(code, client: portal);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Allowed(response, Origin);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var access = tokens.RootElement.GetProperty(Token).GetString()!;
        await UserInfoAsync(portal, access, subject);
        await UserInfoAsync(other, access, null);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, enabled: false, revision: 1);
        await PreflightAsync(portal, allowed: false);
        await UserInfoAsync(portal, access, null);
    }

    /// <summary>Registers callback and origin on different hosts, asserting the protected management response retains both.</summary>
    private static async Task<ManagedCacheTestContext> RegisterAsync(ApplicationHttpFixture http, string client, string origin)
    {
        var body = new ApplicationBrowserCreate(client, Display, [Callback], [origin]);
        using var response = await http.CreateAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), browserProfile: true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<ApplicationDetails>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(Profile, details.Profile);
        Assert.Equal(Callback, Assert.Single(details.RedirectUris));
        Assert.Equal(origin, Assert.Single(details.Origins!));
        Assert.Null(details.Credential);
        return new(http, details.Id, client, Callback, null);
    }

    /// <summary>Creates a cookie-free client with the single serialized Origin a browser fetch sends.</summary>
    private static HttpClient Browser(ApplicationHttpFixture http, string origin)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = http.IssuerClient.BaseAddress };
        client.DefaultRequestHeaders.Add(OriginHeader, origin);
        return client;
    }

    /// <summary>Checks the ordinary GET bearer preflight with no client ID, bearer token or cookies.</summary>
    private static async Task PreflightAsync(HttpClient client, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, UserInfo);
        request.Headers.Add(RequestedMethod, Get);
        request.Headers.Add(RequestedHeaders, AuthorizationHeader);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        if (allowed)
        {
            Allowed(response, client.DefaultRequestHeaders.GetValues(OriginHeader).Single());
        }
        else
        {
            Assert.False(response.Headers.Contains(AllowOrigin));
        }
    }

    /// <summary>Reads discovery and its published JWKS from the registered browser origin.</summary>
    private static async Task DiscoveryAsync(HttpClient client)
    {
        using var discovery = await client.GetAsync(Discovery, TestContext.Current.CancellationToken);
        Allowed(discovery, Origin);
        using var metadata = JsonDocument.Parse(await discovery.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var keys = new Uri(metadata.RootElement.GetProperty(JwksUri).GetString()!);
        Assert.Equal(client.BaseAddress!.Authority, keys.Authority);
        using var response = await client.GetAsync(keys, TestContext.Current.CancellationToken);
        Allowed(response, Origin);
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.NotEmpty(document.RootElement.GetProperty(Keys).EnumerateArray());
    }

    /// <summary>Reads the same bearer token from independently registered origins and verifies the subject for the authorized reader.</summary>
    private static async Task UserInfoAsync(HttpClient client, string access, string? subject)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfo);
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        if (subject is null)
        {
            Denied(response);
            return;
        }
        Allowed(response, Origin);
        await ManagedConsentTest.RequireAsync(response, subject, null, false);
    }

    /// <summary>Requires the single exact origin and a varying response that does not enable credentialed cross-origin calls.</summary>
    private static void Allowed(HttpResponseMessage response, string origin)
    {
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues(AllowOrigin)));
        Assert.Contains(OriginHeader, response.Headers.Vary);
        Assert.False(response.Headers.Contains(AllowCredentials));
    }

    /// <summary>Checks protocol denial without disclosing credentials or returned tokens.</summary>
    private static void Denied(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        Assert.False(response.Headers.Contains(AllowOrigin));
    }
}
