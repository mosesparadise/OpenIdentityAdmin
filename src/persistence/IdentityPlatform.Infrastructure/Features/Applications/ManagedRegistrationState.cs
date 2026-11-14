using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Shares fresh managed registration validation while keeping extended management policy distinct from public issuer eligibility.</summary>
internal sealed record ManagedRegistrationState(ManagedRegistration Registration, ImmutableArray<string> Permissions, bool IsOpenId)
{
    /// <summary>Rejects missing, aliased, system-bound, expired or drifted registrations without auto-adopting native applications.</summary>
    internal static async ValueTask<ManagedRegistrationState?> ReadAsync(ProtocolStorageScope scope, string clientId, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(clientId, token);
        if (application is null || !await ProtocolStorageRows.ReloadAsync(scope, application, token) ||
            await manager.GetClientIdAsync(application, token) != clientId)
        {
            return null;
        }
        var id = await manager.GetIdAsync(application, token);
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength))
        {
            return null;
        }
        try
        {
            await ApplicationMutationWriter.RequireMutableAsync(scope, id!, clientId, token);
        }
        catch (ApplicationMutationRejected) { return null; }
        var policy = await scope.Owner.RunAsync(current => new ValueTask<ApplicationPolicy?>(scope.Owner.Database.Set<ApplicationPolicy>()
            .AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == id, current)), token);
        if (policy is null || !policy.Enabled || policy.ApplicationId != id || policy.Profile != ApplicationEditorRules.Profile ||
            policy.Revision <= 0 || policy.CredentialRevision <= 0 || policy.CredentialIssuedAt > policy.ObservedAt ||
            policy.CredentialExpiresAt <= ManagedLoginRules.Observe(scope.Owner.Clock, policy.ObservedAt))
        {
            return null;
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        return ManagedApplicationProfile.MatchesManagement(descriptor) && !string.IsNullOrEmpty(descriptor.ClientSecret)
            ? new(new(id!, clientId, policy.Revision, policy.CredentialRevision, policy.CredentialExpiresAt, policy.ObservedAt,
                descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToArray()),
                descriptor.Permissions.Order(StringComparer.Ordinal).ToImmutableArray(), ManagedApplicationProfile.Matches(descriptor)) : null;
    }

    /// <summary>Persists a nondecreasing shared authority watermark under governance without advancing the public aggregate version.</summary>
    internal static async ValueTask<ManagedRegistration> ObserveAsync(ProtocolStorageScope scope, ManagedRegistration original, CancellationToken token)
    {
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, original.ObservedAt);
        var changed = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<ApplicationPolicy>()
            .Where(row => row.ApplicationId == original.ApplicationId && row.Enabled && row.Revision == original.Revision && row.CredentialRevision == original.CredentialRevision &&
                row.ObservedAt == original.ObservedAt && row.CredentialExpiresAt == original.ExpiresAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ObservedAt, now), current)), token, write: true);
        var actual = await ManagedRegistration.ReadAsync(scope, original.ClientId, token);
        if (changed != 1 || !original.Matches(actual) || actual!.ObservedAt != now)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return actual;
    }

}
