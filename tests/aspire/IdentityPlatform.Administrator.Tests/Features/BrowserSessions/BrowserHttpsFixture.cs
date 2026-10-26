using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Owns two actual HTTPS hosts, isolated provider stores and retained synthetic keys for genuine browser journeys.</summary>
internal sealed class BrowserHttpsFixture : IAsyncDisposable
{
    private const int FormsPerLogin = 2;
    private const int RedirectsPerLogin = 3;
    private const int ReturnsPerLogin = 2;
    private const string CallbackPath = "/callback";
    private const string KeyId = "browser-https-assertion";
    private const string CleanupFailure = "Browser HTTPS fixture cleanup failed.";
    private readonly NativeClientCredentialFixture material = new();
    private readonly BffSessionFixture tickets = new();
    private readonly BrowserHttpsEndpoint issuerEndpoint = new();
    private readonly BrowserHttpsEndpoint clientEndpoint = new();
    private NativeClientCredentials? keys;
    private SessionProtection? protection;
    private BrowserIssuerHost? issuerHost;
    private BootstrapSessions? database;
    internal BrowserHttpsClient Client { get; private set; } = null!;
    internal AdministratorProtocolScenario Issuer { get; private set; } = null!;
    internal NativeClientProfile Profile { get; private set; } = null!;
    internal int ParRequests => issuerEndpoint.ParRequests;
    internal int TokenRequests => issuerEndpoint.TokenRequests;
    internal int EndSessionGets => issuerEndpoint.EndSessionGets;
    internal int EndSessionPosts => issuerEndpoint.EndSessionPosts;

    /// <summary>Initializes real enrollment and audited registration before any browser receives an account or BFF cookie.</summary>
    internal static async Task<BrowserHttpsFixture> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var owner = new BrowserHttpsFixture();
        try
        {
            owner.keys = NativeClientInteroperabilityTests.Load(owner.material);
            owner.protection = owner.tickets.Protection();
            var ordinary = NativeClientProfile.Create(owner.issuerEndpoint.Address.AbsoluteUri, owner.clientEndpoint.Address.AbsoluteUri,
                new Uri(owner.clientEndpoint.Address, CallbackPath).AbsoluteUri, KeyId, owner.keys);
            owner.Profile = ordinary.ForBrowserSessions();
            owner.Issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, ordinary.Registration, owner.Profile.Issuer);
            Assert.True(await BrowserUpgradeFixture.UpgradeAsync(owner.Issuer, new AdministratorRegistrationInput(Guid.NewGuid(),
                BrowserUpgradeFixture.Operator, ordinary.Registration, 1)));
            owner.database = await fixture.LeaseSessionsAsync(provider);
            owner.issuerHost = await BrowserIssuerHost.CreateHttpsAsync(owner.Issuer.Account.Journey, provider,
                owner.Profile.Registration, owner.Profile.ClientUri, owner.issuerEndpoint);
            owner.Client = await BrowserHttpsClient.CreateAsync(owner.database, owner.Issuer.Account.Journey.Clock,
                owner.Profile, owner.keys, owner.protection, owner.clientEndpoint);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Reconciles actual native transport observations without exposing sensitive request headers or URLs.</summary>
    internal void RequirePolicies(int logins, int logouts, int confirmations, int issuerVisits = 0, int returnReloads = 0)
    {
        clientEndpoint.Policy.Require(logins + logouts, logins * ReturnsPerLogin + confirmations + returnReloads,
            logins * FormsPerLogin + logouts, logins * RedirectsPerLogin);
        issuerEndpoint.Policy.Require(logins * FormsPerLogin + confirmations, logins,
            logins * FormsPerLogin + issuerVisits, logins * RedirectsPerLogin + confirmations);
    }

    /// <summary>Attempts every cleanup in dependency order, joining both network roots before releasing keys or database leases.</summary>
    public async ValueTask DisposeAsync()
    {
        var failed = false;
        try
        {
            if (Client is not null)
            {
                await Client.DisposeAsync();
            }
        }
        catch { failed = true; }
        try
        {
            if (issuerHost is not null)
            {
                await issuerHost.DisposeAsync();
            }
        }
        catch { failed = true; }
        try
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
        }
        catch { failed = true; }
        try
        {
            if (Issuer is not null)
            {
                await Issuer.DisposeAsync();
            }
        }
        catch { failed = true; }
        try
        {
            protection?.Dispose();
        }
        catch { failed = true; }
        try
        {
            keys?.Dispose();
        }
        catch { failed = true; }
        try
        {
            tickets.Dispose();
        }
        catch { failed = true; }
        try
        {
            material.Dispose();
        }
        catch { failed = true; }
        try
        {
            clientEndpoint.Dispose();
        }
        catch { failed = true; }
        try
        {
            issuerEndpoint.Dispose();
        }
        catch { failed = true; }
        Assert.False(failed, CleanupFailure);
    }
}
