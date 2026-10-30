using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Creates the managed registration through genuine protected HTTPS API/BFF and drives the real owned issuer pipeline.</summary>
internal sealed class ManagedCacheTestContext(ApplicationHttpFixture http, ApplicationCredentialOutput created) : IAsyncDisposable
{
    private const string SetCookie = "Set-Cookie";
    private const string RequestUri = "request_uri";
    private const string ClientId = "client_id";
    private const string BrowserCookie = "__Host-managed-browser";
    private const string SecureCookie = "secure";
    private const string HttpOnlyCookie = "httponly";
    private const string RootCookie = "path=/";
    private const string CookieLifetime = "max-age=300";
    private const string SameSiteCookie = "samesite=lax";
    private const string DomainCookie = "domain";
    private const int RedirectParameters = 2;
    internal const string Client = "managed-native-client";
    internal const string Callback = "https://managed.example.test/callback";
    internal ApplicationHttpFixture Http => http;
    internal string Id => created.Application.Id;
    internal string Secret => created.ClientSecret;

    /// <summary>Enables the real managed profile while preserving genuine administrator password/MFA and native credential exchange.</summary>
    internal static async Task<ManagedCacheTestContext> CreateAsync(BootstrapFixture fixture, string provider,
        Action<IServiceCollection>? configure = null, bool enabled = true)
    {
        var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: enabled, configureIssuer: configure);
        try
        {
            using var response = await http.CreateAsync(ApplicationHttpTests.Input(http.Now, Client));
            return new(http, await ApplicationHttpTests.CreatedAsync(response));
        }
        catch { await http.DisposeAsync(); throw; }
    }

    /// <summary>Builds independent native wire intent including fresh nonce/state/verifier without printing any of them.</summary>
    internal static Dictionary<string, string> Initial() => NativeCacheFlow.Initial(NativeCacheProfile.Random(), Client, Callback);

    /// <summary>Uses a real HTTPS connection and the issuer browser's separate cookie jar; redirects are followed only explicitly.</summary>
    internal Task<HttpResponseMessage> SendAsync(Dictionary<string, string> query) =>
        http.IssuerClient.GetAsync(QueryHelpers.AddQueryString(NativeCacheProfile.Authorize,
            query.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))), TestContext.Current.CancellationToken);

    /// <summary>Requires the exact issuer self-redirect, no body and a protected browser cookie, without logging reference material.</summary>
    internal async Task<Dictionary<string, string>> ResumeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var cookies = response.Headers.GetValues(SetCookie).ToArray();
        Assert.True(cookies.Length == 1);
        var cookie = cookies[0].Split(';', StringSplitOptions.TrimEntries);
        Assert.Equal(BrowserCookie, cookie[0].Split('=')[0]);
        cookie = cookie.Skip(1).ToArray();
        Assert.Contains(SecureCookie, cookie, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(HttpOnlyCookie, cookie, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(RootCookie, cookie, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(CookieLifetime, cookie, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(SameSiteCookie, cookie, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(DomainCookie, cookie.Select(value => value.Split('=')[0]), StringComparer.OrdinalIgnoreCase);
        Assert.True(response.Headers.Location?.GetLeftPart(UriPartial.Path) == new Uri(http.Environment.Profile.Issuer, NativeCacheProfile.Authorize).AbsoluteUri);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal(RedirectParameters, query.Count);
        Assert.True(query.TryGetValue(RequestUri, out var reference) && reference.Count == 1 && !string.IsNullOrEmpty(reference[0]));
        Assert.Equal(Client, query[ClientId].Single());
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        return query.ToDictionary(pair => pair.Key, pair => pair.Value.Single()!, StringComparer.Ordinal);
    }

    /// <summary>Reads committed native and product state independently in a fresh provider context.</summary>
    internal async Task<ManagedCacheSnapshot> SnapshotAsync()
    {
        await using var scope = http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<ManagedLoginInteraction>().AsNoTracking().OrderBy(row => row.SelectorDigest).ToArrayAsync(TestContext.Current.CancellationToken);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Include(row => row.Authorization).Where(row => row.Application!.Id == Id)
            .OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return new(rows, tokens, await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync(row => row.Application!.Id == Id, TestContext.Current.CancellationToken),
            await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Requires bounded local unavailable output with no redirect, cookie, credential or state leak.</summary>
    internal static async Task UnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Disposes genuine TLS hosts, synthetic keys and both provider leases in their established order.</summary>
    public ValueTask DisposeAsync() => http.DisposeAsync();
}

/// <summary>Retains test-only native rows in memory; default diagnostic formatting never serializes stored payloads.</summary>
internal sealed class ManagedCacheSnapshot(ManagedLoginInteraction[] interactions, OpenIddictEntityFrameworkCoreToken[] tokens, int authorizations, int sessions)
{
    internal ManagedLoginInteraction[] Interactions => interactions;
    internal OpenIddictEntityFrameworkCoreToken[] Tokens => tokens;
    internal int Authorizations => authorizations;
    internal int Sessions => sessions;
}
