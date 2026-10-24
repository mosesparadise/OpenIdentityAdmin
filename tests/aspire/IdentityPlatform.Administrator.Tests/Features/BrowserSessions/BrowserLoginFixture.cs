using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Owns actual issuer ceremony/native client credentials and one isolated Sessions database for product login controls.</summary>
internal sealed class BrowserLoginFixture : IAsyncDisposable
{
    private const string FactorCode = "Code";
    private const string ResumeDestination = "https://administrator.example.test/bff/login/resume";
    private const string EntryDestination = "https://issuer.example.test/account/administrator/browser/login";
    private const string LoginPath = "/bff/login";
    private const string ResumePath = "/bff/login/resume";
    private const string CookiePath = "/";
    private readonly NativeClientCredentialFixture material = new();
    private readonly BffSessionFixture tickets = new();
    private NativeClientCredentials keys = null!;
    private SessionProtection protection = null!;
    private FactorServices protocol = null!;
    private string provider = string.Empty;
    internal AdministratorProtocolScenario Issuer { get; private set; } = null!;
    internal BootstrapSessions Database { get; private set; } = null!;
    internal NativeClientProfile Profile { get; private set; } = null!;
    internal BrowserLoginHost Host { get; private set; } = null!;
    internal CookieContainer Cookies { get; } = new();
    internal HttpClient Browser { get; private set; } = null!;

    /// <summary>Upgrades the fixed issuer registration through the real command and composes separate browser/native roots.</summary>
    internal static async Task<BrowserLoginFixture> CreateAsync(BootstrapFixture fixture, string provider, Action<IServiceCollection>? configure = null)
    {
        var owner = new BrowserLoginFixture { provider = provider };
        try
        {
            owner.keys = NativeClientInteroperabilityTests.Load(owner.material);
            owner.protection = owner.tickets.Protection();
            var ordinary = NativeClientInteroperabilityTests.Profile(owner.keys);
            owner.Profile = ordinary.ForBrowserSessions();
            owner.Issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, ordinary.Registration);
            Assert.True(await BrowserUpgradeFixture.UpgradeAsync(owner.Issuer, new AdministratorRegistrationInput(Guid.NewGuid(),
                BrowserUpgradeFixture.Operator, ordinary.Registration, 1)));
            owner.protocol = owner.Issuer.CreatePeer(provider, services => services.AddBrowserSessionProtocol(), owner.Profile.Registration);
            owner.Database = await fixture.LeaseSessionsAsync(provider);
            owner.Host = await owner.PeerAsync(configure);
            owner.Browser = owner.Host.Browser(owner.Cookies);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Creates an independent native instance sharing only durable database and caller-owned protected keys.</summary>
    internal Task<BrowserLoginHost> PeerAsync(Action<IServiceCollection>? configure = null) =>
        BrowserLoginHost.CreateAsync(Database, Issuer, protocol.Services, Profile, keys, protection, configure);

    /// <summary>Completes the exact BFF intent through real password and unused MFA forms, returning only its one-use completion selector.</summary>
    internal async Task<string> CompleteAsync(string intent)
    {
        await using var host = await BrowserIssuerHost.CreateAsync(Issuer.Account.Journey, provider);
        using var browser = host.Browser();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, Issuer.Account.Journey, entry);
        var factor = await BrowserIssuerHttp.FactorAsync(browser, password);
        factor[FactorCode] = await IssuerContinuationProviderFixture.UnusedCodeAsync(Issuer.Account.Journey);
        using var complete = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, factor);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        var uri = complete.Headers.Location!;
        Assert.True(uri.IsAbsoluteUri && uri.GetLeftPart(UriPartial.Path) == ResumeDestination);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Single(query);
        return Assert.Single(query[BrowserIssuerHttp.Completion])!;
    }

    /// <summary>Loads real native antiforgery from the inert product form without asserting or logging its secret content.</summary>
    internal async Task<string> EntryAsync(HttpClient? browser = null)
    {
        using var response = await (browser ?? Browser).GetAsync(LoginPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        return AdministratorJourney.Input(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
    }

    /// <summary>Starts through the protected same-origin product POST and accepts only its fixed issuer destination.</summary>
    internal async Task<string> StartAsync(string antiforgery, HttpClient? browser = null)
    {
        using var response = await PostAsync(antiforgery, browser);
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var uri = response.Headers.Location!;
        Assert.True(uri.IsAbsoluteUri && uri.GetLeftPart(UriPartial.Path) == EntryDestination);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Single(query);
        return Assert.Single(query[BrowserIssuerHttp.Intent])!;
    }

    /// <summary>Posts exactly one native antiforgery field; caller faults can independently replace headers or body in their own request.</summary>
    internal Task<HttpResponseMessage> PostAsync(string antiforgery, HttpClient? browser = null) => BrowserIssuerHttp.PostAsync(browser ?? Browser,
        LoginPath, new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = antiforgery });

    /// <summary>Resumes explicitly with only the issuer's completion selector and the browser's actual protected cookies.</summary>
    internal Task<HttpResponseMessage> ResumeAsync(string completion, HttpClient? browser = null) => (browser ?? Browser)
        .GetAsync(QueryHelpers.AddQueryString(ResumePath, BrowserIssuerHttp.Completion, completion), TestContext.Current.CancellationToken);

    /// <summary>Reads a detached durable result through real Sessions-only credentials.</summary>
    internal async Task<T> ReadAsync<T>(Func<AdminSessionDbContext, Task<T>> action)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>());
    }

    /// <summary>Stages a real canonical B1 ticket through T005 for replacement tests only; this does not simulate or claim T011 callback acceptance.</summary>
    internal async Task<(string Handle, string Digest)> EstablishAsync()
    {
        var handle = SessionHandle.Create();
        await ReadAsync(async database =>
        {
            var intent = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(row => row.State == BrowserIntentState.Pending, TestContext.Current.CancellationToken);
            var codec = new SessionTicketCodec(protection.Provider);
            var coordinator = new BrowserLoginCompletion(database, Issuer.Account.Journey.Clock, codec);
            var reservation = BrowserCoordinationFixture.Digest();
            Assert.True(await coordinator.ReserveAsync(intent.BrowserDigest, intent.Digest, intent.Generation, reservation, TestContext.Current.CancellationToken));
            var now = Issuer.Account.Journey.Clock.GetUtcNow();
            var row = SessionTicketPolicy.Create(handle.Digest, BffSessionFixture.Subject, BffSessionFixture.SourceSession,
                now, now.Add(BffSessionFixture.Absolute), BffSessionFixture.Idle, BffSessionFixture.Absolute);
            row.ProtectedTicket = codec.Protect(row, BffSessionFixture.Ticket());
            Assert.True(await coordinator.EstablishAsync(intent.BrowserDigest, intent.Digest, intent.Generation, reservation, row, TestContext.Current.CancellationToken));
            return true;
        });
        Cookies.Add(Profile.ClientUri, new Cookie(IdentityPlatform.Admin.Bff.Features.BrowserSessions.BrowserSessionCookies.Session, handle.Handle, CookiePath) { Secure = true });
        return handle;
    }

    /// <summary>Retains a nonsecret hash of every local login/native/audit effect for exact denial and rollback comparisons.</summary>
    internal Task<string> SnapshotAsync() => ReadAsync(async database => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Contexts = await database.Set<BrowserContext>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(TestContext.Current.CancellationToken),
        Intents = await database.Set<BrowserLoginIntent>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(TestContext.Current.CancellationToken),
        Audits = await database.Set<BrowserSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken),
        Budget = await database.Set<BrowserSessionBudget>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken),
        Tickets = await database.Set<AdminSessionTicket>().AsNoTracking().OrderBy(row => row.Digest)
            .Select(row => new { Row = row, Payload = row.ProtectedTicket, row.RevokedAt }).ToArrayAsync(TestContext.Current.CancellationToken),
        TicketAudits = await database.Set<AdminSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken),
        Tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken)
    }))));

    /// <summary>Disposes browser/host consumers before protocol roots, isolated databases and key material, including partial construction.</summary>
    public async ValueTask DisposeAsync()
    {
        Browser?.Dispose();
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }
        if (protocol is not null)
        {
            await protocol.DisposeAsync();
        }
        if (Database is not null)
        {
            await Database.DisposeAsync();
        }
        if (Issuer is not null)
        {
            await Issuer.DisposeAsync();
        }
        protection?.Dispose();
        keys?.Dispose();
        tickets.Dispose();
        material.Dispose();
    }
}
