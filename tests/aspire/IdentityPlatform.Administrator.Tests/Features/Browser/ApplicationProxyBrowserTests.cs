using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves actual fixed-HTTPS proxy behavior under hostile browser headers and malformed downstream transport.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationProxyBrowserTests(BootstrapFixture fixture)
{
    private const string BeginRead = "() => { window.applicationAbort = new AbortController(); window.applicationPending = fetch('/bff/api/v1/applications', {signal:window.applicationAbort.signal,cache:'no-store'}).then(() => false, e => e.name === 'AbortError'); }";
    private const string AbortRead = "async () => { window.applicationAbort.abort(); const canceled = await window.applicationPending; delete window.applicationAbort; delete window.applicationPending; return canceled; }";
    private const string Collection = "/bff/api/v1/applications";
    private const string Read = "async () => { const r = await fetch('/bff/api/v1/applications',{cache:'no-store',redirect:'error',headers:{Authorization:'Bearer browser-forged', 'X-Identity':'browser-forged'}}); return {status:r.status, body:await r.json()}; }";
    private const string ExpiresAt = "expiresAt";
    private const string Status = "status";
    private const string Body = "body";
    private const string Items = "items";
    private const string ClientId = "clientId";
    private const string Registered = "identity-admin-bff";
    private const string Bearer = "Bearer";
    private const string Canary = "application-response-private-canary";
    private const int Success = 200;
    private const int Unavailable = 503;
    private const int UpperDeadlineSeconds = 8;
    private const int CancellationJoinSeconds = 3;
    private static readonly ApplicationProxyFault[] Faults = [ApplicationProxyFault.Redirect, ApplicationProxyFault.Duplicate,
        ApplicationProxyFault.Unexpected, ApplicationProxyFault.Oversized, ApplicationProxyFault.Malformed, ApplicationProxyFault.Html];

    /// <summary>Only the protected server credential is forwarded; redirect, schema and streaming-body faults release safe zero-record problems with no retries.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealBrowserCredential_When_UpstreamResponsesOrBrowserHeadersAreHostile_Then_ProxyRemainsBoundedAndPrivate(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new ApplicationProxyProbe();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, applicationProbe: probe);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, page);
            await probe.CaptureAsync(environment.Api!.Address, access.Value);
            probe.Fault = ApplicationProxyFault.Valid;
            var baseline = await page.EvaluateAsync<JsonElement>(Read);
            Assert.Equal(Success, baseline.GetProperty(Status).GetInt32());
            Assert.Contains(baseline.GetProperty(Body).GetProperty(Items).EnumerateArray(), row => row.GetProperty(ClientId).GetString() == Registered);
            Assert.True(probe.CredentialsMatch);
            probe.UpstreamExpiry = access.ExpiresAt.AddSeconds(1);
            probe.Fault = ApplicationProxyFault.LaterExpiry;
            var rounded = await page.EvaluateAsync<JsonElement>(Read);
            Assert.Equal(Success, rounded.GetProperty(Status).GetInt32());
            Assert.True(rounded.GetProperty(Body).GetProperty(ExpiresAt).GetDateTimeOffset() <= access.ExpiresAt);
            foreach (var fault in Faults)
            {
                probe.Fault = fault;
                var calls = probe.Requests;
                RequireDenied(await page.EvaluateAsync<JsonElement>(Read), Unavailable, access.Value);
                Assert.Equal(calls + 1, probe.Requests);
                Assert.Equal(0, probe.Redirects);
                Assert.True(probe.CredentialsMatch);
            }
            await RequireCallerAbortAsync(page, probe);
            await RequireDeadlineAsync(page, probe, access.Value);
            await RequireCookieAsync(environment.Profile.ClientUri, access.Value);
        });

    /// <summary>A caller abort reaches the actual downstream request before the server deadline and leaves no retry or late response.</summary>
    private static async Task RequireCallerAbortAsync(IPage page, ApplicationProxyProbe probe)
    {
        probe.BeginDelay();
        var calls = probe.Requests;
        await page.EvaluateAsync(BeginRead);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(CancellationJoinSeconds), TestContext.Current.CancellationToken);
        Assert.True(await page.EvaluateAsync<bool>(AbortRead));
        await probe.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(CancellationJoinSeconds), TestContext.Current.CancellationToken);
        Assert.Equal(calls + 1, probe.Requests);
    }

    /// <summary>An unresponsive real API is canceled at the original budget and receives no automatic retry.</summary>
    private static async Task RequireDeadlineAsync(IPage page, ApplicationProxyProbe probe, string access)
    {
        probe.BeginDelay();
        var calls = probe.Requests;
        var started = Stopwatch.GetTimestamp();
        RequireDenied(await page.EvaluateAsync<JsonElement>(Read), Unavailable, access);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(UpperDeadlineSeconds));
        await probe.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(CancellationJoinSeconds), TestContext.Current.CancellationToken);
        Assert.Equal(calls + 1, probe.Requests);
    }

    /// <summary>A valid API bearer supplied directly by a cookieless browser-side caller cannot establish BFF authentication.</summary>
    private static async Task RequireCookieAsync(Uri client, string access)
    {
        using var transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(client, Collection));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        using var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    /// <summary>Requires a sanitized fixed problem and excludes both genuine credentials and upstream private canaries.</summary>
    private static void RequireDenied(JsonElement result, int expected, string access)
    {
        Assert.Equal(expected, result.GetProperty(Status).GetInt32());
        var body = result.GetProperty(Body);
        Assert.Equal(expected, body.GetProperty(Status).GetInt32());
        Assert.False(body.TryGetProperty(Items, out _));
        Assert.False(body.GetRawText().Contains(access, StringComparison.Ordinal));
        Assert.False(body.GetRawText().Contains(Canary, StringComparison.Ordinal));
    }
}
