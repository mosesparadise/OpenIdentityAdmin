using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly BrowserHttpsEndpoint clientEndpoint;
    private NativeClientCredentials? keys;
    private SessionProtection? protection;
    private BrowserIssuerHost? issuerHost;
    private BootstrapSessions? database;
    internal ApplicationApiHost? Api
    {
        get; private set;
    }
    internal BrowserHttpsClient Client { get; private set; } = null!;
    internal AdministratorProtocolScenario Issuer { get; private set; } = null!;
    internal NativeClientProfile Profile { get; private set; } = null!;
    internal int ParRequests => issuerEndpoint.ParRequests;
    internal int TokenRequests => issuerEndpoint.TokenRequests;
    internal int EndSessionGets => issuerEndpoint.EndSessionGets;
    internal int EndSessionPosts => issuerEndpoint.EndSessionPosts;
    internal int EndSessionRedirects => issuerEndpoint.EndSessionRedirects;
    internal BrowserReturnFailure ReturnFailure => clientEndpoint.ReturnFailure;
    internal BrowserReturnFailure CallbackFailure => clientEndpoint.CallbackFailure;
    internal BrowserNavigationPause ClientNavigation => clientEndpoint.Navigation;
    internal BrowserNavigationPause IssuerNavigation => issuerEndpoint.Navigation;
    internal BrowserStatusObservation Status => clientEndpoint.Status;

    /// <summary>Attaches one passive witness to both real hosts for a single joined browser observation window.</summary>
    internal BrowserResponseWitness ObserveResponses()
    {
        Assert.Null(issuerEndpoint.ResponseWitness);
        Assert.Null(clientEndpoint.ResponseWitness);
        var witness = new BrowserResponseWitness(() =>
        {
            issuerEndpoint.ResponseWitness = null;
            clientEndpoint.ResponseWitness = null;
        });
        issuerEndpoint.ResponseWitness = witness;
        clientEndpoint.ResponseWitness = witness;
        return witness;
    }

    /// <summary>Stages an isolated response-only canary at the actual issuer TLS origin, returning to its already-staged document.</summary>
    internal Uri StageResponseProbe(string cookie, Uri target) => issuerEndpoint.StageResponseProbe(cookie, target);

    /// <summary>Optionally uses the development certificate's loopback IP SAN to separate issuer and BFF cookie hosts and sites.</summary>
    private BrowserHttpsFixture(bool distinctSites)
    {
        clientEndpoint = new(distinctSites);
        clientEndpoint.Policy.PeerOrigin = issuerEndpoint.Address.GetLeftPart(UriPartial.Authority);
        clientEndpoint.Status.PeerOrigin = issuerEndpoint.Address.GetLeftPart(UriPartial.Authority);
        issuerEndpoint.Policy.PeerOrigin = clientEndpoint.Address.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Initializes real enrollment and audited registration before any browser receives an account or BFF cookie.</summary>
    internal static async Task<BrowserHttpsFixture> CreateAsync(BootstrapFixture fixture, string provider,
        Action<IServiceCollection>? configureClient = null, Action<IServiceCollection>? configureIssuer = null, bool distinctSites = false,
        BrowserPilotClient? pilot = null, bool workspace = false, bool applications = false, ApplicationProxyProbe? applicationProbe = null)
    {
        var owner = new BrowserHttpsFixture(distinctSites);
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
                owner.Profile.Registration, owner.Profile.ClientUri, owner.issuerEndpoint, configureIssuer, pilot);
            if (applications)
            {
                owner.Api = await ApplicationApiHost.CreateAsync(owner.Issuer.Account.Journey, provider, owner.Profile.Issuer,
                    new Uri(owner.Profile.ClientUri, CallbackPath).AbsoluteUri, applicationProbe);
            }
            owner.Client = await BrowserHttpsClient.CreateAsync(owner.database, owner.Issuer.Account.Journey.Clock,
                owner.Profile, owner.keys, owner.protection, owner.clientEndpoint, configureClient, workspace, owner.Api?.Address);
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

    /// <summary>Reconciles explicit negative-flow operations while retaining every existing zero-violation policy assertion.</summary>
    internal void RequirePolicies(BrowserHttpsCounts client, BrowserHttpsCounts issuer)
    {
        clientEndpoint.Policy.Require(client.Posts, client.Returns, client.Forms, client.Redirects, client.HostilePosts);
        issuerEndpoint.Policy.Require(issuer.Posts, issuer.Returns, issuer.Forms, issuer.Redirects, issuer.HostilePosts);
    }

    /// <summary>Chooses only one of the two immutable test origins as sender and fixes its sole permitted cross-site peer.</summary>
    internal Uri StageAttacker(Uri origin, string form, bool fetch = false)
    {
        Assert.True(origin == Profile.Issuer || origin == Profile.ClientUri);
        return origin == Profile.Issuer ? issuerEndpoint.StageAttacker(form, clientEndpoint.Address, fetch) :
            clientEndpoint.StageAttacker(form, issuerEndpoint.Address, fetch);
    }

    /// <summary>Joins both API and issuer owners even when one host's cleanup fails.</summary>
    private async Task<bool> DisposeIssuerHostsAsync()
    {
        var failed = false;
        try
        {
            if (Api is not null)
            {
                await Api.DisposeAsync();
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
        return failed;
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
        failed |= await DisposeIssuerHostsAsync();
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
        clientEndpoint.Policy.Report(issuer: false);
        issuerEndpoint.Policy.Report(issuer: true);
        Assert.False(failed, CleanupFailure);
    }
}

/// <summary>Names the exact independently expected native operations for one HTTPS host in a negative browser journey.</summary>
internal sealed record BrowserHttpsCounts(int Posts, int Returns, int Forms, int Redirects, int HostilePosts = 0);
