using System.Net;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal static class DistributionEvidence
{
    internal static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    internal static readonly string[] Nodes = ["auth", "auth-secondary"];

    internal static IEnumerable<TheoryDataRow<string, string, string>> Directions =>
        from provider in Providers
        from source in Nodes
        select new TheoryDataRow<string, string, string>(provider, source, Other(source)).WithTrait("Provider", provider);

    internal static string Other(string node) => node == "auth" ? "auth-secondary" : "auth";

    internal static async Task<TestFlow> SilentAsync(IssuerEnvironment environment, string node, CookieContainer cookies,
        TestFlow original, string? error = null)
    {
        using var browser = environment.NodeClient(node, cookies);
        var flow = environment.RelyingParty.Prepare(prompt: "none");
        flow.ExpectedSessionId = original.ExpectedSessionId;
        flow.ExpectedAuthenticatedAt = original.ExpectedAuthenticatedAt;
        var uri = environment.RelyingParty.StartUri(flow);
        for (var hop = 0; hop < 8; hop++)
        {
            Assert.True(NodeTransport.SameOrigin(uri, environment.Issuer) || NodeTransport.SameOrigin(uri, environment.RelyingParty.BaseUri));
            Assert.True(uri.AbsolutePath is "/start" or "/connect/authorize" or "/callback", "Silent cross-node flow attempted an interactive page.");
            using var response = await browser.GetAsync(uri, TestContext.Current.CancellationToken);
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out var headers) && headers.Any(value =>
                value.StartsWith(PilotProfile.SessionCookie + "=", StringComparison.Ordinal) && !value.StartsWith(PilotProfile.SessionCookie + "=;", StringComparison.Ordinal)),
                "Silent cross-node flow replaced its application ticket.");
            if (response.Headers.Location is { } location)
            {
                Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther);
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            Assert.True(uri.AbsolutePath == "/callback" && response.StatusCode == HttpStatusCode.OK, "Silent flow failed before its protected callback.");
            var completed = await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.True(error is null ? completed && flow.Code.Length > 0 && flow.Error.Length == 0 :
                !completed && flow.Code.Length == 0 && flow.Error == error, "Unexpected silent protocol decision.");
            if (error is null)
            {
                using var tokens = await RedeemAsync(environment, node, flow);
            }
            return flow;
        }
        throw new InvalidOperationException("Silent navigation exceeded its bound.");
    }

    internal static async Task<JsonDocument> RedeemAsync(IssuerEnvironment environment, string node, TestFlow flow)
    {
        using var transport = environment.NodeClient(node);
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, transport: transport,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        try
        {
            Assert.True((await environment.RelyingParty.ValidateAsync(tokens.RootElement, flow, TestContext.Current.CancellationToken, transport)).Length == 0,
                "Cross-node tokens failed independent validation through the selected node's public metadata.");
            return tokens;
        }
        catch
        {
            tokens.Dispose();
            throw;
        }
    }

    internal static async Task NativeErrorAsync(HttpResponseMessage response, string identifier)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.TryGetProperty("error_uri", out var error) &&
            error.GetString() == "https://documentation.openiddict.com/errors/" + identifier, "Unexpected native denial boundary.");
    }
}
