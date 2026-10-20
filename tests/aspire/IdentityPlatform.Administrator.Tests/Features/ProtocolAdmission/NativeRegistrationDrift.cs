using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Models explicit external registration drift in disposable data; no product update/repair operation is implied.</summary>
internal static class NativeRegistrationDrift
{
    private const string AlternateCallback = "https://native-assertion.example.test/other-callback";
    private const string AlternateResource = "unregistered-resource";
    private const string AlternateFingerprint = "external-fixture-key-fingerprint";
    internal enum Change
    {
        Disabled, ManifestKey, ApplicationRedirect, ScopeResource
    }

    /// <summary>Serializes the selected valid native metadata change or manifest intervention under the actual profile owner.</summary>
    internal static Task ApplyAsync(AdministratorProtocolScenario scenario, Change change) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
                switch (change)
                {
                    case Change.Disabled:
                        await scope.Owner.RunAsync(async current => Assert.Equal(1, await scope.Owner.Database.Set<AdministratorProtocolProfile>()
                            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, false), current)), token, write: true);
                        break;
                    case Change.ManifestKey:
                        await scope.Owner.RunAsync(async current => Assert.Equal(1, await scope.Owner.Database.Set<AdministratorProtocolProfile>()
                            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PublicKeyFingerprint, AlternateFingerprint), current)), token, write: true);
                        break;
                    case Change.ApplicationRedirect:
                        await ChangeApplicationAsync(scope, token);
                        break;
                    case Change.ScopeResource:
                        await ChangeScopeAsync(scope, token);
                        break;
                }
                await transaction.CommitAsync(token);
                return true;
            });

    /// <summary>Uses the native manager to change only the application's callback while preserving its assertion key and protocol permissions.</summary>
    private static async Task ChangeApplicationAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
        Assert.NotNull(application);
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        descriptor.RedirectUris.Clear();
        descriptor.RedirectUris.Add(new Uri(AlternateCallback));
        await manager.UpdateAsync(application, descriptor, token);
    }

    /// <summary>Uses the native scope manager to persist a mismatching resource without changing the registered scope's identity.</summary>
    private static async Task ChangeScopeAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var scopeRow = await manager.FindByNameAsync(AdministratorProtocolIdentity.Scope, token);
        Assert.NotNull(scopeRow);
        var descriptor = new OpenIddictScopeDescriptor();
        await manager.PopulateAsync(descriptor, scopeRow, token);
        descriptor.Resources.Clear();
        descriptor.Resources.Add(AlternateResource);
        await manager.UpdateAsync(scopeRow, descriptor, token);
    }

    /// <summary>Hashes independently read persisted policy and identities, excluding the lock-only counter that each legitimate lookup advances.</summary>
    internal static async Task<string> SnapshotAsync(AdministratorProtocolScenario scenario)
    {
        await using var scope = scenario.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().Select(row => new
        {
            row.Id,
            row.ApplicationId,
            row.ScopeId,
            row.RedirectUri,
            row.PublicKeyFingerprint,
            row.Enabled,
            row.Revision,
            row.ObservedAt
        }).SingleAsync(token);
        var applications = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.ClientId, row.ClientType, row.Permissions, row.Requirements, row.RedirectUris, row.JsonWebKeySet, row.Settings, row.Properties }).ToArrayAsync(token);
        var scopes = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Name, row.Resources, row.Properties }).ToArrayAsync(token);
        return FactorBinding.Digest(new
        {
            profile,
            applications,
            scopes
        });
    }
}
