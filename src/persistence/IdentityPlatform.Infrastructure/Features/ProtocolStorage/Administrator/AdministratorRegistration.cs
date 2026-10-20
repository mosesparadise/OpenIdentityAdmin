using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Reads the complete current native registration inside the caller's serialized profile transaction.</summary>
internal static class AdministratorRegistration
{
    /// <summary>Requires an active write epoch and uncached native managers; the caller retains profile ownership through its final commit.</summary>
    internal static async ValueTask<AdministratorRegistrationBinding?> ReadLockedAsync(ProtocolStorageScope scope,
        AdministratorRegistrationPolicy expected, CancellationToken token)
    {
        var owner = scope.Owner;
        var phase = owner.Capture(write: true);
        var profile = await owner.RunAsync(current => new ValueTask<AdministratorProtocolProfile?>(owner.Database
            .Set<AdministratorProtocolProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, current)), token);
        if (profile is not { Enabled: true, Revision: > 0 } || profile.RedirectUri != expected.RedirectUri || profile.PublicKeyFingerprint != expected.Key.Fingerprint)
        {
            return null;
        }
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var application = await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
        var resource = await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, token);
        if (application is null || resource is null || !await ProtocolStorageRows.ReloadAsync(scope, application, token) || !await ProtocolStorageRows.ReloadAsync(scope, resource, token) ||
            await applications.GetIdAsync(application, token) != profile.ApplicationId ||
            await scopes.GetIdAsync(resource, token) != profile.ScopeId)
        {
            return null;
        }
        var applicationDescriptor = new OpenIddictApplicationDescriptor();
        var scopeDescriptor = new OpenIddictScopeDescriptor();
        await applications.PopulateAsync(applicationDescriptor, application, token);
        await scopes.PopulateAsync(scopeDescriptor, resource, token);
        owner.Check(phase, token);
        return expected.Matches(applicationDescriptor, scopeDescriptor) ? new(profile.ApplicationId, profile.ScopeId, profile.Revision, profile.ObservedAt) : null;
    }

}

/// <summary>Identifies the just-verified registration only; this snapshot is not a reusable authority grant.</summary>
internal sealed record AdministratorRegistrationBinding(string ApplicationId, string ScopeId, long Revision, long ObservedAt);
