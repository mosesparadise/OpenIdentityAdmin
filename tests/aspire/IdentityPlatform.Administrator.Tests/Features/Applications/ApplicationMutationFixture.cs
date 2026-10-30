using System.Globalization;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Uses actual enrollment/session and manager-created native lifecycle rows for current-authority component tests; it does not claim HTTP bearer validation.</summary>
internal sealed class ApplicationMutationFixture(NativeAssertionMaterial material, AdministratorProtocolScenario scenario, ServiceProvider api) : IAsyncDisposable
{
    internal const string Client = "managed-write-test";
    internal const string Display = "Managed write test";
    internal const string Redirect = "https://managed.example.test/callback";
    private const int LifetimeSeconds = 1200;
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    internal ApiIdentity Identity { get; private set; } = null!;
    internal AdministratorProtocolScenario Scenario => scenario;
    internal ServiceProvider Api => api;
    internal FactorClock Clock => scenario.Account.Journey.Clock;
    internal CancellationToken Token { get; } = TestContext.Current.CancellationToken;

    /// <summary>Owns only the existing real provider/journey fixture and an independent API root without issuer private keys or factor codecs.</summary>
    internal static async Task<ApplicationMutationFixture> CreateAsync(BootstrapFixture fixture, string provider, ApplicationWriteFault? fault = null, ApplicationMutationCleanupFault? cleanup = null)
    {
        var material = new NativeAssertionMaterial(browser: true);
        AdministratorProtocolScenario? scenario = null;
        ServiceProvider? api = null;
        try
        {
            scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, material.Policy);
            var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
                .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), scenario.Account.Journey.Lease.RuntimeConnection)
                .AddSingleton<TimeProvider>(scenario.Account.Journey.Clock).AddApplicationReads(scenario.Issuer, material.Policy.RedirectUri);
            if (fault is not null)
            {
                services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(fault));
            }
            cleanup?.Configure(services);
            api = services.BuildServiceProvider(validateScopes: true);
            var result = new ApplicationMutationFixture(material, scenario, api);
            await result.SeedNativeAsync();
            return result;
        }
        catch
        {
            if (api is not null)
            {
                await api.DisposeAsync();
            }
            if (scenario is not null)
            {
                await scenario.DisposeAsync();
            }
            material.Dispose();
            throw;
        }
    }

    /// <summary>Creates native lifecycle records through managers, explicitly identifying this seam as component setup rather than a signed token acceptance test.</summary>
    private async Task SeedNativeAsync()
    {
        await using var scope = Scope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(Token);
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = profile.ApplicationId,
            Subject = scenario.Account.ActorId,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, Token);
        var authorizationId = (await authorizations.GetIdAsync(authorization, Token))!;
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var expiry = Clock.Now.AddSeconds(LifetimeSeconds);
        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = profile.ApplicationId,
            AuthorizationId = authorizationId,
            Subject = scenario.Account.ActorId,
            Type = OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid,
            CreationDate = Clock.Now.UtcDateTime,
            ExpirationDate = expiry.UtcDateTime
        }, Token);
        Identity = new(scenario.Account.ActorId, scenario.Account.Grant.SessionId, (await tokens.GetIdAsync(token, Token))!, authorizationId, expiry);
    }

    /// <summary>Creates a deliberately ordinary setup/observation scope; production commands always acquire their own Owned child.</summary>
    internal AsyncServiceScope Scope()
    {
        var scope = api.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }

    /// <summary>Freezes an exact future expiry using the test's controlled product clock and independently named profile inputs.</summary>
    internal ApplicationMutationCommand Create(string client = Client, int lifetime = LifetimeSeconds) => ApplicationMutationCommand.Create(
        new(client, Display, [Redirect], Clock.Now.AddSeconds(lifetime).ToString(ExpiryFormat, CultureInfo.InvariantCulture)), Clock.Now)!;

    /// <summary>Creates an explicit credential-replacement command without reading or replaying a prior secret.</summary>
    internal ApplicationMutationCommand Replace(ApplicationMutationResult previous) => ApplicationMutationCommand.Replace(previous.Application!.Id, previous.Version!,
        new(true, Clock.Now.AddSeconds(LifetimeSeconds).ToString(ExpiryFormat, CultureInfo.InvariantCulture)), Clock.Now)!;

    /// <summary>Starts one original API operation budget and invokes the actual coordinator with no synthetic authorization callback.</summary>
    internal Task<ApplicationMutationResult> ExecuteAsync(ApplicationMutationCommand command) => ExecuteWithCancellationAsync(command, Token);

    /// <summary>Combines explicit fault cancellation with the original test token rather than replacing test ownership.</summary>
    internal async Task<ApplicationMutationResult> ExecuteWithCancellationAsync(ApplicationMutationCommand command, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(Token, cancellation);
        using var budget = new ApplicationReadBudget(Clock, linked.Token);
        return await ApplicationMutationOperation.ExecuteAsync(api.GetRequiredService<IServiceScopeFactory>(), Identity, command,
            api.GetRequiredService<ApiResourceProfile>(), Clock, budget, Guid.NewGuid());
    }

    /// <summary>Compares actual native hash acceptance without disclosing either generated plaintext in assertion diagnostics.</summary>
    internal async Task<bool> AcceptsAsync(string id, string secret)
    {
        await using var scope = Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var row = await manager.FindByIdAsync(id, Token);
        return row is not null && await manager.ValidateClientSecretAsync(row, secret, Token);
    }

    /// <summary>Counts only this feature's successful intents through a fresh detached observation.</summary>
    internal async Task<int> AuditsAsync()
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .CountAsync(row => row.TargetType == AuditTarget, Token);
    }
    private const string AuditTarget = "Application";

    /// <summary>Releases the API graph before genuine account resources and disposable assertion material.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await api.DisposeAsync();
        }
        finally
        {
            try
            {
                await scenario.DisposeAsync();
            }
            finally { material.Dispose(); }
        }
    }
}
