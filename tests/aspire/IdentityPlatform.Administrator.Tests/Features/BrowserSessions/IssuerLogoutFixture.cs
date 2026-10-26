using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses genuine fresh password/MFA, native callback and issuer cookies before protected native end-session.</summary>
internal sealed class IssuerLogoutFixture : IAsyncDisposable
{
    private const int SelectorBytes = 32;
    private const string ClientValue = "identity-admin-bff";
    private const string ReturnPath = "/bff/logout/return";
    private const string AuditEvent = "administrator-logout";
    private const string Accepted = "accepted";
    private const string ActorType = "account";
    private const string TargetType = "administrator-account";
    private const string SetCookie = "Set-Cookie";
    private const string DeletedCookie = "__Host-identity-administrator=;";

    internal const string Path = "/connect/end-session";
    internal const string Confirmation = "confirmation";
    internal const string State = "state";
    internal const string Hint = "logout_hint";
    internal const string Client = "client_id";
    internal const string Return = "post_logout_redirect_uri";
    internal BrowserLoginFixture Login { get; private set; } = null!;
    internal BrowserIssuerHost Host { get; private set; } = null!;
    internal HttpClient Browser { get; private set; } = null!;
    internal string PreCallbackGeneration { get; private set; } = string.Empty;
    internal string Source { get; private set; } = string.Empty;
    private string subject = string.Empty;
    private string native = string.Empty;
    internal Dictionary<string, string> Fields { get; private set; } = [];

    /// <summary>Reuses the real audited upgrade and fresh browser journey; no account principal or logout authority is fabricated.</summary>
    internal static async Task<IssuerLogoutFixture> CreateAsync(BootstrapFixture fixture, string provider,
        Action<IServiceCollection>? configure = null, params IInterceptor[] faults)
    {
        var owner = new IssuerLogoutFixture();
        try
        {
            owner.Login = await BrowserLoginFixture.CreateAsync(fixture, provider);
            var callbackUri = await owner.Login.CallbackAsync();
            owner.PreCallbackGeneration = owner.Login.Cookies.GetCookies(owner.Login.Profile.ClientUri)[IdentityPlatform.Admin.Bff.Features.BrowserSessions.BrowserSessionCookies.Generation]!.Value;
            using var callback = await owner.Login.Browser.GetAsync(callbackUri, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
            owner.Source = await owner.Login.ReadAsync(db => db.Set<AdminSessionTicket>().Select(row => row.SourceSession).SingleAsync(TestContext.Current.CancellationToken));
            owner.subject = await owner.Login.ReadAsync(db => db.Set<AdminSessionTicket>().Select(row => row.Subject).SingleAsync(TestContext.Current.CancellationToken));
            owner.Host = await BrowserIssuerHost.CreateLogoutAsync(owner.Login.Issuer.Account.Journey, provider, owner.Login.Profile.Registration, configure, faults);
            owner.Browser = owner.Host.Browser(owner.Login.IssuerCookies);
            owner.Fields = new()
            {
                [Client] = ClientValue,
                [Return] = new Uri(owner.Login.Profile.ClientUri, ReturnPath).AbsoluteUri,
                [Hint] = owner.Source,
                [State] = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(SelectorBytes))
            };
            owner.native = await owner.NativeSnapshotAsync();
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Loads actual native confirmation fields and preserves secret form material only in memory.</summary>
    internal async Task<Dictionary<string, string>> FormAsync(HttpClient? browser = null)
    {
        using var page = await GetAsync(browser: browser);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        BrowserFormHeaders.Logout(page, Login.Profile.ClientUri);
        Assert.True(page.Headers.CacheControl?.NoStore == true);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return Fields.Keys.Append(Confirmation).Append(AdministratorJourney.Antiforgery)
            .ToDictionary(name => name, name => AdministratorJourney.Input(html, name), StringComparer.Ordinal);
    }

    /// <summary>Submits exact native GET values with no automatic redirect following.</summary>
    internal Task<HttpResponseMessage> GetAsync(Dictionary<string, string>? fields = null, HttpClient? browser = null) =>
        (browser ?? Browser).GetAsync(QueryHelpers.AddQueryString(Path, (fields ?? Fields).ToDictionary(pair => pair.Key, pair => (string?)pair.Value)), TestContext.Current.CancellationToken);

    /// <summary>Submits the actual native antiforgery form with an exact same-origin header.</summary>
    internal Task<HttpResponseMessage> PostAsync(Dictionary<string, string> form, HttpClient? browser = null, string? origin = null) =>
        BrowserIssuerHttp.PostAsync(browser ?? Browser, Path, form, origin);

    /// <summary>Reads every source's revocation state and all logout audits using a fresh runtime context.</summary>
    internal async Task<(Dictionary<string, bool> Sessions, int Audits)> EffectsAsync()
    {
        await using var scope = Login.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await database.Set<AdministratorSession>().AsNoTracking().ToDictionaryAsync(row => row.PublicId, row => row.Revoked, TestContext.Current.CancellationToken),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == AuditEvent && row.Outcome == Accepted, TestContext.Current.CancellationToken));
    }

    /// <summary>Confirms one exact source and its atomic audit independently of any returned browser URL.</summary>
    internal async Task RequireRevokedAsync()
    {
        var effects = await EffectsAsync();
        Assert.True(effects.Sessions[Source]);
        Assert.True(effects.Sessions.Where(pair => pair.Key != Source).All(pair => !pair.Value));
        Assert.Equal(1, effects.Audits);
        await using var scope = Login.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Event == AuditEvent, TestContext.Current.CancellationToken);
        Assert.True(audit.ActorId == subject && audit.TargetId == subject);
        Assert.Equal(ActorType, audit.ActorType);
        Assert.Equal(TargetType, audit.TargetType);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.True(audit.OccurredAt > 0 && audit.OccurredAt <= Login.Issuer.Account.Journey.Clock.GetUtcNow().ToUnixTimeSeconds());
        Assert.Equal(native, await NativeSnapshotAsync());
        await BrowserAuditHistory.RequireIssuerLogoutAsync(this);
    }

    /// <summary>Hashes actual native token/authorization state so remote logout cannot silently revoke unrelated grants or mint credentials.</summary>
    private async Task<string> NativeSnapshotAsync()
    {
        await using var scope = Login.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var value = new
        {
            Tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken)
        };
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)));
    }

    /// <summary>Checks the native state-only return without printing selector-bearing locations.</summary>
    internal async Task RequireReturnAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        BrowserFormHeaders.Referrer(response);
        var uri = response.Headers.Location;
        Assert.True(uri is { IsAbsoluteUri: true } && uri.GetLeftPart(UriPartial.Path) == Fields[Return] && string.IsNullOrEmpty(uri.Fragment));
        var query = QueryHelpers.ParseQuery(uri!.Query);
        Assert.True(query.Count == 1 && query[State].Count == 1 && query[State][0] == Fields[State]);
        Assert.True(response.Headers.Contains(SetCookie));
        var deleted = response.Headers.GetValues(SetCookie).Any(value => value.StartsWith(DeletedCookie, StringComparison.Ordinal));
        Assert.True(deleted);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Releases request consumers before host, database leases and durable synthetic key owners.</summary>
    public async ValueTask DisposeAsync()
    {
        Browser?.Dispose();
        try
        {
            if (Host is not null)
            {
                await Host.DisposeAsync();
            }
        }
        finally { if (Login is not null) { await Login.DisposeAsync(); } }
    }
}
