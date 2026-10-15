using System.Net;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("transport-acceptance")]
public sealed partial class TransportAcceptanceTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Encodings = [false, true];
    private const string ClientId = "identityplatform-pilot";
    private static readonly (string Case, int Status, string Error, string Reason)[] ClientCases =
    [
        ("missing-header", 400, "invalid_request", "ID2029"),
        ("body-id-without-secret", 401, "invalid_client", "ID2198"),
        ("empty-basic", 400, "invalid_request", "ID2029"),
        ("empty-basic-pair", 400, "invalid_request", "ID2029"),
        ("empty-basic-id", 400, "invalid_request", "ID2029"),
        ("empty-basic-secret", 401, "invalid_client", "ID2198"),
        ("malformed-basic", 400, "invalid_request", "ID2055"),
        ("basic-without-colon", 400, "invalid_request", "ID2055"),
        ("body-only", 401, "invalid_client", "ID2174"),
        ("basic-and-body-secret", 401, "invalid_client", "ID2174"),
        ("conflicting-body-id", 400, "invalid_request", "identity-conflict"),
        ("case-conflicting-body-id", 400, "invalid_request", "identity-conflict"),
        ("duplicate-body-id", 400, "invalid_request", "identity-conflict")
    ];
    private static readonly string[] Grants = ["missing", "empty", "password", "client_credentials", "refresh_token", "urn:ietf:params:oauth:grant-type:device_code", "unregistered-grant"];

    public static IEnumerable<TheoryDataRow<string, string, int, string, string>> ClientAuthenticationCases =>
        from provider in Providers
        from entry in ClientCases
        select new TheoryDataRow<string, string, int, string, string>(provider, entry.Case, entry.Status, entry.Error, entry.Reason).WithTrait("Provider", provider);

    public static IEnumerable<TheoryDataRow<string, string>> GrantCases =>
        from provider in Providers
        from grant in Grants
        select new TheoryDataRow<string, string>(provider, grant).WithTrait("Provider", provider);

    public static IEnumerable<TheoryDataRow<string, bool>> MatchingIdentities =>
        from provider in Providers
        from encoded in Encodings
        select new TheoryDataRow<string, bool>(provider, encoded).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(ClientAuthenticationCases))]
    public async Task Token_client_authentication_rejects_the_exact_invalid_boundary(string provider, string variant, int status, string error, string reason)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        var values = TokenValues(environment, code.Flow);
        var authorization = Basic(environment);
        switch (variant)
        {
            case "missing-header":
                authorization = null;
                break;
            case "body-id-without-secret":
                authorization = null;
                values.Add(new("client_id", ClientId));
                break;
            case "empty-basic":
                authorization = "Basic";
                break;
            case "empty-basic-pair":
                authorization = EncodeBasic(":");
                break;
            case "empty-basic-id":
                authorization = EncodeBasic(":" + environment.ClientSecret);
                break;
            case "empty-basic-secret":
                authorization = EncodeBasic(ClientId + ":");
                break;
            case "malformed-basic":
                authorization = "Basic !!!";
                break;
            case "basic-without-colon":
                authorization = EncodeBasic(ClientId);
                break;
            case "body-only":
                authorization = null;
                values.Add(new("client_id", ClientId));
                values.Add(new("client_secret", environment.ClientSecret));
                break;
            case "basic-and-body-secret":
                values.Add(new("client_secret", environment.ClientSecret));
                break;
            case "conflicting-body-id":
                values.Add(new("client_id", "unregistered-client"));
                break;
            case "case-conflicting-body-id":
                values.Add(new("client_id", "IDENTITYPLATFORM-PILOT"));
                break;
            case "duplicate-body-id":
                values.Add(new("client_id", ClientId));
                values.Add(new("client_id", "unregistered-client"));
                break;
            default:
                Assert.Fail("Unknown client-authentication case.");
                break;
        }
        using var request = TokenRequest(environment, values, authorization);
        await DeniedExchangeAsync(environment, browser, code, request, (HttpStatusCode)status, error, reason);
    }

    [Theory]
    [MemberData(nameof(GrantCases))]
    public async Task Only_the_authorization_code_grant_is_accepted(string provider, string grant)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        var values = TokenValues(environment, code.Flow);
        values.RemoveAll(value => value.Key == "grant_type");
        if (grant != "missing")
        {
            values.Add(new("grant_type", grant == "empty" ? "" : grant));
        }
        using var request = TokenRequest(environment, values, Basic(environment));
        var absent = grant is "missing" or "empty";
        await DeniedExchangeAsync(environment, browser, code, request, HttpStatusCode.BadRequest,
            absent ? "invalid_request" : "unsupported_grant_type", absent ? "ID2029" : "ID2032");
    }

    [Theory]
    [MemberData(nameof(MatchingIdentities))]
    public async Task Matching_body_identity_preserves_native_Basic_decoding(string provider, bool encoded)
    {
        var environment = await fixture.GetAsync(provider, httpProbe: true);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        var values = TokenValues(environment, flow);
        values.Add(new("client_id", ClientId));
        using var request = TokenRequest(environment, values, Basic(environment, encoded));
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        await RequireTokensAsync(environment, response, flow);
    }

    private static async Task DeniedExchangeAsync(IssuerEnvironment environment, HttpClient browser, ExchangeEvidence code,
        HttpRequestMessage request, HttpStatusCode status, string error, string reason)
    {
        await code.RequireFreshAsync(environment);
        var before = await TransportSnapshot.CaptureAsync(environment);
        using var transport = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        // Demonstrate a real authenticated bypass before failing, rather than treating an arbitrary 200 as issuance.
        if (response.IsSuccessStatusCode)
        {
            await RequireTokensAsync(environment, response, code.Flow);
            Assert.Fail("Protocol boundary bypass: invalid request issued independently validated tokens.");
        }
        await RequireDenialAsync(environment, response, status, error, reason, json: true, head: request.Method == HttpMethod.Head, code.Flow,
            request.Headers.Authorization?.Parameter);
        before.RequireUnchanged(await TransportSnapshot.CaptureAsync(environment));
        await code.RequireFreshAsync(environment);
        await RecoverAsync(environment, browser);
    }

    private static List<KeyValuePair<string, string>> TokenValues(IssuerEnvironment environment, TestFlow flow) =>
    [
        new("grant_type", "authorization_code"), new("code", flow.Code),
        new("code_verifier", flow.Verifier), new("redirect_uri", environment.RelyingParty.Callback)
    ];

    private static string EncodeBasic(string value) => "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(value));
    private static string Basic(IssuerEnvironment environment, bool encoded = false) => EncodeBasic((encoded ? "%69dentityplatform-pilot" : ClientId) + ":" + environment.ClientSecret);

    private static HttpRequestMessage TokenRequest(IssuerEnvironment environment, List<KeyValuePair<string, string>> values, string? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(environment.Issuer, "/connect/token")) { Content = new FormUrlEncodedContent(values) };
        if (authorization is not null)
        {
            Assert.True(request.Headers.TryAddWithoutValidation("Authorization", authorization), "Could not construct the intended authentication header.");
        }
        return request;
    }

    private static async Task RecoverAsync(IssuerEnvironment environment, HttpClient browser) =>
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated, "A fresh eligible flow must recover after rejection.");

    private static async Task RequireTokensAsync(IssuerEnvironment environment, HttpResponseMessage response, TestFlow flow)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = ParseObject(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(string.IsNullOrEmpty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, flow, TestContext.Current.CancellationToken)),
            "The allowed request must issue independently validated tokens.");
    }

    private static async Task RequireDenialAsync(IssuerEnvironment environment, HttpResponseMessage response, HttpStatusCode status,
        string error, string reason, bool json, bool head = false, TestFlow? flow = null, string? basicCredentials = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.Location is null, "Early protocol rejection must not redirect.");
        Assert.True(response.Headers.CacheControl?.NoStore == true && response.Headers.Pragma.Any(value => value.Name == "no-cache"), "Protocol rejection must prohibit caching.");
        Assert.True(status == HttpStatusCode.Unauthorized
            ? response.Headers.WwwAuthenticate.Any(value => value.Scheme == "Basic")
            : response.Headers.WwwAuthenticate.Count == 0, "Unexpected authentication challenge at the rejected boundary.");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var captured = text + string.Join("\n", response.Headers.Select(value => value.Key + ":" + string.Join(",", value.Value)));
        SafeAssertions.ContainsNoSensitiveMaterial(captured, new[] { environment.Password, environment.ClientSecret, flow?.Code ?? "", flow?.Verifier ?? "", flow?.State ?? "", flow?.Nonce ?? "", basicCredentials ?? "" }
            .Where(value => value.Length != 0).ToArray());
        if (head)
        {
            Assert.True(text.Length == 0, "HEAD must not return an entity body.");
            return;
        }
        if (json)
        {
            Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", "Token error must use the native JSON representation.");
            using var body = ParseObject(text);
            Assert.True(body.RootElement.TryGetProperty("error", out var actual) && actual.GetString() == error, "Unexpected native protocol error.");
            foreach (var name in new[] { "code", "access_token", "id_token", "refresh_token" })
            {
                Assert.False(body.RootElement.TryGetProperty(name, out _), "A rejected request must not include issued credentials.");
            }
            if (reason == "identity-conflict")
            {
                Assert.True(body.RootElement.GetProperty("error_description").GetString() == "The client identifiers are inconsistent.", "The identity-conflict guard must reject this request.");
            }
            else
            {
                Assert.True(body.RootElement.TryGetProperty("error_uri", out var uri) && uri.GetString() == "https://documentation.openiddict.com/errors/" + reason,
                    "The expected native validation boundary must reject the request.");
            }
        }
        else
        {
            Assert.True(response.Content.Headers.ContentType?.MediaType == "text/plain", "Unvalidated authorization errors must remain local text responses.");
            Assert.True(text.Contains("error:" + error, StringComparison.Ordinal) && text.Contains("error_uri:https://documentation.openiddict.com/errors/" + reason, StringComparison.Ordinal),
                "The expected authorization extraction boundary must reject the request.");
        }
    }

    private static JsonDocument ParseObject(string text)
    {
        try
        {
            var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }
            document.Dispose();
            Assert.Fail("The protocol response must be a JSON object.");
        }
        catch (JsonException)
        {
            Assert.Fail("The protocol response must contain valid JSON.");
        }
        throw new InvalidOperationException("Unreachable response assertion.");
    }
}
