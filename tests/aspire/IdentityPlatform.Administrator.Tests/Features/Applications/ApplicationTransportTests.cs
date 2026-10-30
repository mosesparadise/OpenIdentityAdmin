using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves durable native JSON binding and fixed real TLS forwarding on both providers; this does not replace retained-engine browser acceptance.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationTransportTests(BootstrapFixture fixture)
{
    private const string PathSeparator = "/";
    private const string CookieHeader = "Set-Cookie";
    private const string PrivateHeader = "X-Private";
    private const string Client = "transport-client";
    private const string Display = "Transport client";
    private const string Edited = "Transport edited";
    private const string Redirect = "https://managed.example.test/callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Replacement = "/secret-replacements";
    private const string NativeCookie = "__Host-identity-admin-antiforgery";
    private const string Missing = "invalid-native-token";
    private const string ForeignOrigin = "https://foreign.example.test";
    private const string Canary = "private-canary";
    private const string Secret = "clientSecret";
    private const int NextRevision = 2;
    private const int FinalRevision = 3;
    private static readonly string[] Faults = ["redirect", "duplicate", "unknown", "oversized", "html", "client", "fields", "etag-id", "revision", "fractional", "abort"];

    /// <summary>All three actions preserve exact method/path/precondition/body and sanitized output; hostile real upstream responses cause exactly one attempt.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineSession_When_FiniteMutationsAndHostileResponsesUseTls_Then_OnlyBoundedExactResultsArePublished(string provider)
    {
        await using var test = await ApplicationTransportFixture.CreateAsync(fixture, provider);
        var csrf = await test.TokenAsync();
        var create = Create(test.Now);
        using var created = await test.SendAsync(csrf, HttpMethod.Post, ApplicationTransportFixture.Collection, create);
        await RequireAsync(created, HttpStatusCode.Created, secret: true);
        Assert.Equal(ApplicationVersion.Create(ApplicationMutationProbe.Id, 1), created.Headers.ETag!.ToString());
        var path = ApplicationTransportFixture.Collection + PathSeparator + ApplicationMutationProbe.Id;
        using var edited = await test.SendAsync(csrf, HttpMethod.Put, path, JsonSerializer.Serialize(new
        {
            displayName = Edited,
            redirectUris = new[] { Redirect }
        }), created.Headers.ETag.ToString());
        await RequireAsync(edited, HttpStatusCode.OK, secret: false);
        Assert.Equal(ApplicationVersion.Create(ApplicationMutationProbe.Id, NextRevision), edited.Headers.ETag!.ToString());
        using var replaced = await test.SendAsync(csrf, HttpMethod.Post, path + Replacement,
            JsonSerializer.Serialize(new
            {
                confirmed = true,
                credentialExpiresAt = test.Now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture)
            }), edited.Headers.ETag.ToString());
        await RequireAsync(replaced, HttpStatusCode.OK, secret: true);
        Assert.Equal(ApplicationVersion.Create(ApplicationMutationProbe.Id, FinalRevision), replaced.Headers.ETag!.ToString());
        foreach (var fault in Faults)
        {
            test.Probe.Fault = fault;
            var calls = test.Probe.Requests;
            using var denied = await test.SendAsync(csrf, HttpMethod.Post, ApplicationTransportFixture.Collection, create);
            await RequireAsync(denied, HttpStatusCode.ServiceUnavailable, secret: false);
            Assert.Equal(calls + 1, test.Probe.Requests);
            Assert.True(test.Probe.SafeRequest);
            Assert.Equal(0, test.Probe.Redirects);
            Assert.Null(test.Login.Cookies.GetCookies(test.Login.Profile.ClientUri)[Canary]);
        }
        await RequireDeadlineAsync(test, csrf, create);
    }

    /// <summary>A frozen wall clock cannot extend the original monotonic budget; the actual TLS upstream observes cancellation with no second send.</summary>
    private static async Task RequireDeadlineAsync(ApplicationTransportFixture test, string csrf, string body)
    {
        const string Pause = "pause";
        const int MaximumSeconds = 8;
        const int JoinSeconds = 3;
        test.Probe.Fault = Pause;
        var calls = test.Probe.Requests;
        var started = Stopwatch.GetTimestamp();
        using var denied = await test.SendAsync(csrf, HttpMethod.Post, ApplicationTransportFixture.Collection, body);
        await RequireAsync(denied, HttpStatusCode.ServiceUnavailable, secret: false);
        var remaining = TimeSpan.FromSeconds(MaximumSeconds) - Stopwatch.GetElapsedTime(started);
        Assert.True(remaining > TimeSpan.Zero);
        var join = remaining < TimeSpan.FromSeconds(JoinSeconds) ? remaining : TimeSpan.FromSeconds(JoinSeconds);
        await test.Probe.Canceled.Task.WaitAsync(join, TestContext.Current.CancellationToken);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(MaximumSeconds));
        Assert.Equal(calls + 1, test.Probe.Requests);
    }

    /// <summary>Actual native token acquisition does not renew state; foreign origin, form-purpose and old-generation tokens cause zero TLS sends.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurableGeneration_When_JsonProofIsForeignOrStale_Then_NoUpstreamMutationIsSent(string provider)
    {
        await using var test = await ApplicationTransportFixture.CreateAsync(fixture, provider);
        var before = await test.Login.SnapshotAsync();
        var csrf = await test.TokenAsync();
        Assert.Equal(before, await test.Login.SnapshotAsync());
        var form = await test.Login.EntryAsync();
        var cookie = test.Login.Cookies.GetCookies(test.Login.Profile.ClientUri)[NativeCookie]!;
        var retained = new Cookie(cookie.Name, cookie.Value, cookie.Path) { Secure = true, HttpOnly = true };
        var body = Create(test.Now);
        foreach (var value in new[] { Missing, form })
        {
            using var denied = await test.SendAsync(value, HttpMethod.Post, ApplicationTransportFixture.Collection, body);
            await RequireAsync(denied, HttpStatusCode.BadRequest, secret: false);
        }
        using var foreign = await test.SendAsync(csrf, HttpMethod.Post, ApplicationTransportFixture.Collection, body, origin: ForeignOrigin);
        await RequireAsync(foreign, HttpStatusCode.BadRequest, secret: false);
        Assert.Equal(0, test.Probe.Requests);
        using var completed = await test.Login.Browser.GetAsync(await test.Login.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, completed.StatusCode);
        await test.RefreshCredentialAsync();
        test.Login.Cookies.Add(test.Login.Profile.ClientUri, retained);
        using var stale = await test.SendAsync(csrf, HttpMethod.Post, ApplicationTransportFixture.Collection, body);
        await RequireAsync(stale, HttpStatusCode.BadRequest, secret: false);
        Assert.Equal(0, test.Probe.Requests);
        using var valid = await test.SendAsync(await test.TokenAsync(), HttpMethod.Post, ApplicationTransportFixture.Collection, body);
        await RequireAsync(valid, HttpStatusCode.Created, secret: true);
        Assert.Equal(1, test.Probe.Requests);
    }

    /// <summary>Uses independent wire fields and a deliberately chosen future UTC second.</summary>
    private static string Create(DateTimeOffset now) => JsonSerializer.Serialize(new
    {
        clientId = Client,
        displayName = Display,
        redirectUris = new[] { Redirect },
        credentialExpiresAt = now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture)
    });

    /// <summary>Checks sanitized nonredirecting output and that neither upstream private headers nor cookies are forwarded.</summary>
    private static async Task RequireAsync(HttpResponseMessage response, HttpStatusCode status, bool secret)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(CookieHeader));
        Assert.False(response.Headers.Contains(PrivateHeader));
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(secret, document.RootElement.TryGetProperty(Secret, out _));
        Assert.False(document.RootElement.GetRawText().Contains(Canary, StringComparison.Ordinal));
    }
}
