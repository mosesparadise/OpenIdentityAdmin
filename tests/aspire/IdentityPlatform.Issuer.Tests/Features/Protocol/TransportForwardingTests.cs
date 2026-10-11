using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class TransportAcceptanceTests
{
    private static readonly string[] ForwardingHeaders = ["forwarded", "x-forwarded", "combined"];
    private static readonly string[] HttpHeaders = ["none", "forwarded", "x-forwarded", "combined"];

    public static IEnumerable<TheoryDataRow<string, string>> ForwardingCases =>
        from provider in Providers
        from headers in ForwardingHeaders
        select new TheoryDataRow<string, string>(provider, headers).WithTrait("Provider", provider);

    public static IEnumerable<TheoryDataRow<string, string>> HttpCases =>
        from provider in Providers
        from headers in HttpHeaders
        select new TheoryDataRow<string, string>(provider, headers).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(ForwardingCases))]
    public async Task Untrusted_forwarding_cannot_change_HTTPS_discovery_redirects_cookies_or_tokens(string provider, string headers)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var navigation = new ProtocolNavigation(environment, headers);
        using var browser = new HttpClient(navigation) { Timeout = TimeSpan.FromSeconds(15) };
        using var metadataResponse = await browser.GetAsync(new Uri(environment.Issuer, "/.well-known/openid-configuration"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var metadata = ParseObject(await metadataResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        foreach (var endpoint in new[] { ("issuer", "/"), ("authorization_endpoint", "/connect/authorize"), ("token_endpoint", "/connect/token"), ("jwks_uri", "/connect/jwks") })
        {
            Assert.True(metadata.RootElement.GetProperty(endpoint.Item1).GetString() == new Uri(environment.Issuer, endpoint.Item2).AbsoluteUri,
                "Untrusted forwarding must not alter canonical discovery endpoints.");
        }
        var flow = await environment.AuthorizeAsync(browser);
        Assert.True(navigation.CachedRedirect && navigation.CallbackRedirect && navigation.ProtectedCookie,
            "The real flow must preserve native caching, the registered callback and protected issuer cookies.");
        using var request = TokenRequest(environment, TokenValues(environment, flow), Basic(environment));
        AddForwarding(request, headers, httpProbe: false);
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        await RequireTokensAsync(environment, response, flow);
    }

    [Theory]
    [MemberData(nameof(HttpCases))]
    public async Task Untrusted_forwarding_cannot_upgrade_plaintext_to_HTTPS(string provider, string headers)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        Assert.True(environment.HttpProbe is { IsLoopback: true, Scheme: "http" } && environment.Issuer.Scheme == "https",
            "Only the test probe may expose plaintext; the canonical issuer must remain HTTPS.");
        var before = await TransportSnapshot.CaptureAsync(environment);
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        foreach (var endpoint in new[] { "authorize", "token" })
        {
            // Deliberately credential-free: the precise HTTPS error distinguishes this from missing-authentication denial.
            using var request = new HttpRequestMessage(endpoint == "token" ? HttpMethod.Post : HttpMethod.Get,
                new Uri(environment.HttpProbe!, "/connect/" + endpoint));
            if (endpoint == "token")
            {
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code" });
            }
            AddForwarding(request, headers, httpProbe: true);
            using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
            await RequireDenialAsync(environment, response, HttpStatusCode.BadRequest, "invalid_request", "ID2083", json: endpoint == "token");
            before.RequireUnchanged(await TransportSnapshot.CaptureAsync(environment));
        }
        using var browser = IssuerEnvironment.CreateBrowserClient();
        await RecoverAsync(environment, browser);
    }

    private static void AddForwarding(HttpRequestMessage request, string headers, bool httpProbe)
    {
        var protocol = httpProbe ? "https" : "http";
        if (headers is "forwarded" or "combined")
        {
            request.Headers.Add("Forwarded", "for=192.0.2.1;proto=" + protocol + ";host=untrusted.example");
        }
        if (headers is "x-forwarded" or "combined")
        {
            request.Headers.Add("X-Forwarded-Proto", headers == "combined" ? (httpProbe ? "http" : "https") : protocol);
            request.Headers.Add("X-Forwarded-Host", "untrusted.example");
            request.Headers.Add("X-Forwarded-Port", "81");
            request.Headers.Add("X-Forwarded-Prefix", "/untrusted");
            request.Headers.Add("X-Forwarded-For", "192.0.2.1");
        }
    }

    private sealed class ProtocolNavigation(IssuerEnvironment environment, string headers, string? authorizationContentType = null)
        : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
    {
        public bool PostedAuthorization
        {
            get; private set;
        }
        public bool CachedRedirect
        {
            get; private set;
        }
        public bool CallbackRedirect
        {
            get; private set;
        }
        public bool ProtectedCookie
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var owned = new List<HttpRequestMessage>();
            HttpResponseMessage? pending = null;
            try
            {
                for (var redirects = 0; redirects < 10; redirects++)
                {
                    var uri = request.RequestUri!;
                    var allowedIssuer = SameOrigin(uri, environment.Issuer) && uri.AbsolutePath is "/.well-known/openid-configuration" or "/connect/authorize" or "/account/login" or "/account/complete";
                    var allowedClient = SameOrigin(uri, environment.RelyingParty.BaseUri) && uri.AbsolutePath is "/start" or "/callback";
                    Assert.True((allowedIssuer || allowedClient) && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0,
                        "Refusing to send a request to a noncanonical test destination.");
                    if (SameOrigin(uri, environment.Issuer))
                    {
                        if (authorizationContentType is not null && !PostedAuthorization && uri.AbsolutePath == "/connect/authorize")
                        {
                            Assert.True(request.Method == HttpMethod.Get && !QueryHelpers.ParseQuery(uri.Query).ContainsKey("request_uri"), "The form control must replace only the original authorization GET.");
                            request.Method = HttpMethod.Post;
                            request.RequestUri = new Uri(environment.Issuer, "/connect/authorize");
                            request.Content = new FormUrlEncodedContent(QueryHelpers.ParseQuery(uri.Query).SelectMany(value => value.Value.Select(item => new KeyValuePair<string, string>(value.Key, item!))));
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(authorizationContentType);
                            PostedAuthorization = true;
                        }
                        AddForwarding(request, headers, httpProbe: false);
                    }
                    var response = await base.SendAsync(request, cancellationToken);
                    pending = response;
                    if (SameOrigin(uri, environment.Issuer) && response.Headers.TryGetValues("Set-Cookie", out var cookies))
                    {
                        foreach (var cookie in cookies)
                        {
                            var attributes = cookie.Split(';', StringSplitOptions.TrimEntries);
                            Assert.True(cookie.StartsWith("__Host-", StringComparison.Ordinal) && cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase) &&
                                cookie.Contains("; httponly", StringComparison.OrdinalIgnoreCase) && !cookie.Contains("; domain=", StringComparison.OrdinalIgnoreCase) &&
                                attributes.Contains("path=/", StringComparer.OrdinalIgnoreCase) && attributes.Contains("samesite=lax", StringComparer.OrdinalIgnoreCase),
                                "Untrusted headers must not weaken host-cookie protections.");
                            ProtectedCookie = true;
                        }
                    }
                    if (response.StatusCode is not HttpStatusCode.Redirect and not HttpStatusCode.SeeOther)
                    {
                        pending = null;
                        return response;
                    }
                    var location = response.Headers.Location;
                    Assert.True(location is not null, "A redirect must name its destination.");
                    var next = location.IsAbsoluteUri ? location : new Uri(request.RequestUri!, location);
                    var issuer = SameOrigin(next, environment.Issuer) && next.AbsolutePath is "/connect/authorize" or "/account/login" or "/account/complete";
                    var callback = SameOrigin(next, environment.RelyingParty.BaseUri) && next.AbsolutePath == "/callback";
                    Assert.True((issuer || callback) && next.UserInfo.Length == 0 && next.Fragment.Length == 0,
                        "Refusing to navigate to a noncanonical protocol destination.");
                    CachedRedirect |= issuer && next.AbsolutePath == "/connect/authorize" && QueryHelpers.ParseQuery(next.Query).ContainsKey("request_uri");
                    CallbackRedirect |= callback;
                    response.Dispose();
                    pending = null;
                    request = new HttpRequestMessage(HttpMethod.Get, next);
                    owned.Add(request);
                }
                throw new InvalidOperationException("The protocol exceeded its bounded redirect count.");
            }
            finally
            {
                pending?.Dispose();
                foreach (var item in owned)
                {
                    item.Dispose();
                }
            }
        }

        private static bool SameOrigin(Uri first, Uri second) => first.GetLeftPart(UriPartial.Authority) == second.GetLeftPart(UriPartial.Authority);
    }
}
