using System.Globalization;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses genuine password/MFA, durable provider sessions and browser fetch against the fixed TLS transport; upstream writes are an explicit response seam.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationMutationBrowserTests(BootstrapFixture fixture)
{
    private const string Collection = "/bff/api/v1/applications";
    private const string ApiCollection = "/api/v1/applications";
    private const string Version = "version";
    private const string Put = "PUT";
    private const string Post = "POST";
    private const string Replacement = "/secret-replacements";
    private const string Separator = "/";

    private const string MalformedToken = "malformed-native-token";
    private const string FormTokenSelector = "input[name=__RequestVerificationToken]";
    private const string CsrfCookie = "__Host-identity-admin-antiforgery";
    private const string Pause = "pause";
    private const string ClientName = "transport-client";
    private const string DisplayName = "Transport client";
    private const string Callback = "https://managed.example.test/callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string PrivateField = "private";
    private const string Canary = "private-canary";

    private const string Acquire = "async () => { const r=await fetch('/bff/api/v1/applications/antiforgery',{cache:'no-store'}); return {status:r.status,cache:r.headers.get('Cache-Control'),body:await r.json()}; }";
    private const string Send = "async arg => { const headers={'Content-Type':'application/json','X-CSRF-TOKEN':arg.token,'X-Identity':'forged'}; if(arg.version)headers['If-Match']=arg.version; const r=await fetch(arg.path??'/bff/api/v1/applications',{method:arg.method??'POST',headers,body:JSON.stringify(arg.body),redirect:'error'}); return {status:r.status,version:r.headers.get('ETag'),cache:r.headers.get('Cache-Control'),private:r.headers.get('X-Private'),body:await r.json()}; }";
    private const string Begin = "arg => { window.pendingMutation = (" + Send + ")(arg); }";
    private const string Finish = "async () => { const r=await window.pendingMutation; delete window.pendingMutation; return r; }";
    private const string TokenField = "requestToken";
    private const string BodyField = "body";
    private const string StatusField = "status";
    private const string SecretField = "clientSecret";
    private const string CacheField = "cache";
    private const string NoStore = "no-store";
    private const int Success = 200;
    private const int Unauthorized = 401;
    private const int Created = 201;
    private const int Denied = 400;
    private const int Unavailable = 503;
    private const int JoinSeconds = 3;
    private static readonly string[] Faults = ["redirect", "duplicate", "unknown", "oversized", "html", "client", "fields", "etag-id", "revision", "fractional", "abort"];

    /// <summary>Real token acquisition does not renew Sessions; current-generation JSON succeeds while native form-purpose and malformed tokens never dispatch.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_AuthenticatedBrowser_When_ApplicationTokensCrossPurposeOrGeneration_Then_OnlyCurrentBoundNativeTokenDispatches(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new ApplicationMutationProbe();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, mutationProbe: probe);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, page);
            probe.Access = access.Value;
            probe.Now = environment.Issuer.Account.Journey.Clock.Now;
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var token = await TokenAsync(page);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            var body = Input(probe.Now);
            var created = await page.EvaluateAsync<JsonElement>(Send, new
            {
                token,
                body
            });
            Require(created, Created, access.Value);
            await RequireActionsAsync(page, probe, token, created);
            var calls = probe.Requests;
            Require(await page.EvaluateAsync<JsonElement>(Send, new
            {
                token = MalformedToken,
                body
            }), Denied, access.Value);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
            var formToken = await page.Locator(FormTokenSelector).InputValueAsync();
            Require(await page.EvaluateAsync<JsonElement>(Send, new
            {
                token = formToken,
                body
            }), Denied, access.Value);
            Assert.Equal(calls, probe.Requests);
            await RequireForeignOriginAsync(environment, page, probe);
            await RequireNextGenerationAsync(environment, page, probe, token, body);
        });

    /// <summary>Every hostile upstream response is one attempt with no private output; actual logout during a paused response suppresses its committed-result analogue.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealJsonTransport_When_UpstreamSubstitutesOrGenerationChanges_Then_NoSecretOrRetryEscapes(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new ApplicationMutationProbe();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, mutationProbe: probe);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, page);
            probe.Access = access.Value;
            probe.Now = environment.Issuer.Account.Journey.Clock.Now;
            var token = await TokenAsync(page);
            var body = Input(probe.Now);
            foreach (var fault in Faults)
            {
                probe.Fault = fault;
                var calls = probe.Requests;
                Require(await page.EvaluateAsync<JsonElement>(Send, new
                {
                    token,
                    body
                }), Unavailable, access.Value);
                Assert.Equal(calls + 1, probe.Requests);
                Assert.True(probe.SafeRequest);
                Assert.Equal(0, probe.Redirects);
                Assert.DoesNotContain(await page.Context.CookiesAsync(), cookie => cookie.Name == Canary);
            }
            await RequirePublicationRaceAsync(environment, page, probe, token, body, access.Value);
        });

    /// <summary>Exercises actual browser PUT/replacement and independently observes their exact downstream target and strong precondition.</summary>
    private static async Task RequireActionsAsync(IPage page, ApplicationMutationProbe probe, string token, JsonElement created)
    {
        var path = Collection + Separator + ApplicationMutationProbe.Id;
        var version = created.GetProperty(Version).GetString()!;
        probe.ExpectedMethod = Put;
        probe.ExpectedPath = ApiCollection + Separator + ApplicationMutationProbe.Id;
        probe.ExpectedVersion = version;
        var edited = await page.EvaluateAsync<JsonElement>(Send, new
        {
            token,
            method = Put,
            path,
            version,
            body = new
            {
                displayName = DisplayName,
                redirectUris = new[] { Callback }
            }
        });
        Assert.Equal(Success, edited.GetProperty(StatusField).GetInt32());
        Assert.False(edited.GetProperty(BodyField).TryGetProperty(SecretField, out _));
        version = edited.GetProperty(Version).GetString()!;
        probe.ExpectedMethod = Post;
        probe.ExpectedPath += Replacement;
        probe.ExpectedVersion = version;
        var replaced = await page.EvaluateAsync<JsonElement>(Send, new
        {
            token,
            method = Post,
            path = path + Replacement,
            version,
            body = new
            {
                confirmed = true,
                credentialExpiresAt = probe.Now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture)
            }
        });
        Assert.Equal(Success, replaced.GetProperty(StatusField).GetInt32());
        Assert.True(replaced.GetProperty(BodyField).TryGetProperty(SecretField, out _));
        Assert.True(probe.SafeRequest);
        Assert.DoesNotContain(await page.Context.CookiesAsync(), cookie => cookie.Name == Canary);
        probe.ExpectedMethod = Post;
        probe.ExpectedPath = ApiCollection;
        probe.ExpectedVersion = null;
    }

    /// <summary>Uses a genuine document at the issuer origin to send an ambient-cookie simple POST; the BFF rejects browser-generated foreign provenance before dispatch.</summary>
    private static async Task RequireForeignOriginAsync(BrowserHttpsFixture environment, IPage page, ApplicationMutationProbe probe)
    {
        const string Attack = "async target => { await fetch(target, {method:'POST',mode:'no-cors',credentials:'include',headers:{'Content-Type':'text/plain'},body:'{}'}); }";
        var attacker = await page.Context.NewPageAsync();
        try
        {
            await attacker.GotoAsync(environment.Profile.Issuer.AbsoluteUri);
            var target = new Uri(environment.Profile.ClientUri, Collection).AbsoluteUri;
            var calls = probe.Requests;
            var response = await attacker.RunAndWaitForResponseAsync(() => attacker.EvaluateAsync(Attack, target), value => value.Url == target);
            Assert.Equal(Denied, response.Status);
            Assert.Equal(calls, probe.Requests);
        }
        finally { await attacker.CloseAsync(); }
    }

    /// <summary>Retains an actual prior antiforgery cookie across a new genuine sign-in so generation rejection cannot pass merely because logout removed that cookie.</summary>
    private static async Task RequireNextGenerationAsync(BrowserHttpsFixture environment, IPage page, ApplicationMutationProbe probe, string oldToken, object body)
    {
        var saved = Assert.Single(await page.Context.CookiesAsync(), cookie => cookie.Name == CsrfCookie);
        await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
        var access = await ApplicationAccessFixture.SignInAsync(environment, page);
        probe.Access = access.Value;
        probe.Now = environment.Issuer.Account.Journey.Clock.Now;
        await page.Context.AddCookiesAsync([new Cookie { Name = saved.Name, Value = saved.Value, Domain = saved.Domain, Path = saved.Path, Secure = saved.Secure, HttpOnly = saved.HttpOnly, SameSite = saved.SameSite }]);
        var calls = probe.Requests;
        Require(await page.EvaluateAsync<JsonElement>(Send, new
        {
            token = oldToken,
            body
        }), Denied, access.Value);
        Assert.Equal(calls, probe.Requests);
        var token = await TokenAsync(page);
        Require(await page.EvaluateAsync<JsonElement>(Send, new
        {
            token,
            body
        }), Created, access.Value);
        Assert.True(probe.SafeRequest);
    }

    /// <summary>Leaves the initiating document alive while another real tab logs out, then verifies output suppression after one observed upstream send.</summary>
    private static async Task RequirePublicationRaceAsync(BrowserHttpsFixture environment, IPage page, ApplicationMutationProbe probe, string token, object body, string access)
    {
        probe.Fault = Pause;
        var calls = probe.Requests;
        await page.EvaluateAsync(Begin, new
        {
            token,
            body
        });
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(JoinSeconds), TestContext.Current.CancellationToken);
        var other = await page.Context.NewPageAsync();
        try
        {
            await BrowserHttpsJourney.LogoutAsync(environment, other, issuer: false);
        }
        finally { probe.Release.TrySetResult(); }
        Require(await page.EvaluateAsync<JsonElement>(Finish), Unauthorized, access);
        Assert.Equal(calls + 1, probe.Requests);
    }

    /// <summary>Checks the actual native token GET's minimal no-store JSON without displaying the token in diagnostics.</summary>
    private static async Task<string> TokenAsync(IPage page)
    {
        var result = await page.EvaluateAsync<JsonElement>(Acquire);
        Assert.Equal(Success, result.GetProperty(StatusField).GetInt32());
        Assert.Equal(NoStore, result.GetProperty(CacheField).GetString());
        Assert.Single(result.GetProperty(BodyField).EnumerateObject());
        return result.GetProperty(BodyField).GetProperty(TokenField).GetString()!;
    }

    /// <summary>Provides a finite explicit operator command, independent of production serializers.</summary>
    private static object Input(DateTimeOffset now) => new { clientId = ClientName, displayName = DisplayName, redirectUris = new[] { Callback }, credentialExpiresAt = now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture) };

    /// <summary>Asserts finite status and privacy without logging response contents, genuine OAuth values or successful one-time output.</summary>
    private static void Require(JsonElement result, int status, string access)
    {
        Assert.Equal(status, result.GetProperty(StatusField).GetInt32());
        Assert.Equal(NoStore, result.GetProperty(CacheField).GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty(PrivateField).ValueKind);
        var body = result.GetProperty(BodyField);
        Assert.False(body.GetRawText().Contains(access, StringComparison.Ordinal));
        Assert.False(body.GetRawText().Contains(Canary, StringComparison.Ordinal));
        Assert.Equal(status == Created, body.TryGetProperty(SecretField, out _));
    }
}
