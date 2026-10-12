using System.Net;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal sealed class CachedRequestEvidence(TestFlow flow, CookieContainer cookies, Uri target)
{
    public TestFlow Flow => flow;
    public Uri Target => target;
    public CookieContainer Cookies => cookies;
    public string Reference => QueryHelpers.ParseQuery(target.Query)["request_uri"].ToString()[RequestUris.Prefixes.Generic.Length..];

    public static async Task<CachedRequestEvidence> CreateAsync(IssuerEnvironment host, string node)
    {
        var flow = host.RelyingParty.Prepare();
        var cookies = new CookieContainer();
        using var browser = host.NodeClient(node, cookies);
        using var start = await browser.GetAsync(host.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var authorization = start.Headers.Location!;
        Assert.True(NodeTransport.SameOrigin(authorization, host.Issuer));
        using var response = await browser.GetAsync(authorization, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var target = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(host.Issuer, response.Headers.Location);
        Assert.True(NodeTransport.SameOrigin(target, host.Issuer) && target.AbsolutePath == "/connect/authorize");
        var reference = QueryHelpers.ParseQuery(target.Query)["request_uri"].ToString();
        Assert.True(reference.StartsWith(RequestUris.Prefixes.Generic, StringComparison.Ordinal), "Cached request lacks native reference prefix.");
        return new CachedRequestEvidence(flow, cookies, target);
    }

    public async Task CompleteAsync(IssuerEnvironment host, string node)
    {
        using var browser = host.NodeClient(node, cookies, autoRedirect: true);
        using var page = await browser.GetAsync(target, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("/account/login", page.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        flow.InteractionId = IssuerEnvironment.InputValue(html, "Interaction");
        var login = new TestLogin
        {
            Flow = flow,
            Interaction = flow.InteractionId,
            Antiforgery = IssuerEnvironment.InputValue(html, "__RequestVerificationToken")
        };
        var continuation = await login.CompleteAsync(host, browser);
        await login.FollowAsync(host, browser, continuation);
        using var tokens = await DistributionEvidence.RedeemAsync(host, node, flow);
    }

    public async Task RequireDeniedAsync(IssuerEnvironment host, string node, string nativeError = "ID2004")
    {
        var before = await RestartEvidence.StateAsync(host);
        using var browser = host.NodeClient(node, cookies);
        using var response = await browser.GetAsync(target, TestContext.Current.CancellationToken);
        // Native cached request validation cannot trust a callback recovered from an undecryptable payload.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Location is null, "Rejected cached request unexpectedly redirected.");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        IdentityPlatform.Testing.SafeAssertions.ContainsNoSensitiveMaterial(text,
            new[] { Reference, flow.Code, flow.Verifier, flow.State, host.Password, host.ClientSecret }.Where(value => value.Length > 0).ToArray());
        Assert.True(response.Content.Headers.ContentType?.MediaType == "text/plain", "Unexpected native local-error response type.");
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
        Assert.True(lines.Contains("error:" + Errors.InvalidToken, StringComparer.Ordinal) &&
            lines.Contains("error_uri:https://documentation.openiddict.com/errors/" + nativeError, StringComparer.Ordinal), "Unexpected native cached-request denial.");
        Assert.All(lines, line => Assert.True(line.StartsWith("error:", StringComparison.Ordinal) ||
            line.StartsWith("error_description:", StringComparison.Ordinal) || line.StartsWith("error_uri:", StringComparison.Ordinal), "Native error includes unexpected fields."));
        await RestartEvidence.RequireUnchangedAsync(host, before);
        Assert.False(flow.Completion.Task.IsCompleted);
    }
}
