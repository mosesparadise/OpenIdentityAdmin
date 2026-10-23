using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;

/// <summary>Retains exact freshly loaded native metadata while allowing only the supported disabled profile flag.</summary>
internal sealed record AdministratorUpgradeSnapshot(AdministratorProtocolProfile Profile, object Application,
    OpenIddictApplicationDescriptor Descriptor, OpenIddictScopeDescriptor Scope)
{
    /// <summary>Requires existing profile/native identity binding without promoting disabled state to live admission.</summary>
    internal static async ValueTask<AdministratorUpgradeSnapshot> ReadAsync(ProtocolStorageScope scope, AdministratorRegistrationPolicy policy, CancellationToken token)
    {
        var profile = await scope.Owner.RunAsync(current => new ValueTask<AdministratorProtocolProfile?>(scope.Owner.Database
            .Set<AdministratorProtocolProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, current)), token);
        AdministratorRegistrationUpgrade.Require(profile is { Revision: > 0, ObservedAt: >= 0 } && profile.RedirectUri == policy.RedirectUri &&
            profile.PublicKeyFingerprint == policy.Key.Fingerprint);
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var application = await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
        var resource = await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, token);
        AdministratorRegistrationUpgrade.Require(application is not null && resource is not null &&
            await ProtocolStorageRows.ReloadAsync(scope, application, token) && await ProtocolStorageRows.ReloadAsync(scope, resource, token) &&
            await applications.GetIdAsync(application, token) == profile!.ApplicationId && await scopes.GetIdAsync(resource, token) == profile.ScopeId);
        var descriptor = new OpenIddictApplicationDescriptor();
        var resourceDescriptor = new OpenIddictScopeDescriptor();
        await applications.PopulateAsync(descriptor, application!, token);
        await scopes.PopulateAsync(resourceDescriptor, resource!, token);
        return new(profile!, application!, descriptor, resourceDescriptor);
    }

    /// <summary>Changes only the reviewed manifest delta, retaining display metadata and all existing native identity/key bindings.</summary>
    internal async ValueTask ApplyAsync(ProtocolStorageScope scope, AdministratorRegistrationPolicy browser, CancellationToken token)
    {
        var target = browser.Application();
        Descriptor.Permissions.UnionWith(target.Permissions);
        Descriptor.PostLogoutRedirectUris.UnionWith(target.PostLogoutRedirectUris);
        Descriptor.Properties[AdministratorRegistrationPolicy.ProfileProperty] = target.Properties[AdministratorRegistrationPolicy.ProfileProperty];
        await scope.Services.GetRequiredService<IOpenIddictApplicationManager>().UpdateAsync(Application, Descriptor, token);
    }
}
