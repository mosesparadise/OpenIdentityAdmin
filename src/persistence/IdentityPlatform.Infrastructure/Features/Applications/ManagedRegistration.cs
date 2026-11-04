using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Reads an exact current managed native registration under the same governance serialization used by editor writes.</summary>
internal sealed record ManagedRegistration(string ApplicationId, string ClientId, long Revision, long CredentialRevision, long ExpiresAt,
    long ObservedAt, string[] Redirects)
{
    /// <summary>Acquires governance before any managed policy or native registration read; no account or administrator capability is granted.</summary>
    internal static ValueTask LockAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(current =>
        new ValueTask(new CeremonyLocks((PlatformDbContext)scope.Owner.Database).AcquireAsync(new CeremonyLockPlan(true, false, [], []), current)), token, write: true);

    /// <summary>Rejects missing, aliased, system-bound, expired or drifted registrations without auto-adopting native applications.</summary>
    internal static async ValueTask<ManagedRegistration?> ReadAsync(ProtocolStorageScope scope, string clientId, CancellationToken token)
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
        if (policy is null || policy.ApplicationId != id || policy.Profile != ApplicationEditorRules.Profile ||
            policy.Revision <= 0 || policy.CredentialRevision <= 0 || policy.CredentialIssuedAt > policy.ObservedAt ||
            policy.CredentialExpiresAt <= ManagedLoginRules.Observe(scope.Owner.Clock, policy.ObservedAt))
        {
            return null;
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        return ManagedApplicationProfile.Matches(descriptor) && !string.IsNullOrEmpty(descriptor.ClientSecret)
            ? new(id!, clientId, policy.Revision, policy.CredentialRevision, policy.CredentialExpiresAt, policy.ObservedAt,
                descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToArray()) : null;
    }

    /// <summary>Persists a nondecreasing shared authority watermark under governance without advancing the public aggregate version.</summary>
    internal async ValueTask<ManagedRegistration> ObserveAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, ObservedAt);
        var changed = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<ApplicationPolicy>()
            .Where(row => row.ApplicationId == ApplicationId && row.Revision == Revision && row.CredentialRevision == CredentialRevision &&
                row.ObservedAt == ObservedAt && row.CredentialExpiresAt == ExpiresAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ObservedAt, now), current)), token, write: true);
        var actual = await ReadAsync(scope, ClientId, token);
        if (changed != 1 || !Matches(actual) || actual!.ObservedAt != now)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return actual;
    }

    /// <summary>Checks immutable registration authority without record-array reference equality or logging native credentials.</summary>
    internal bool Matches(ManagedRegistration? current) => current is not null && ApplicationId == current.ApplicationId &&
        ClientId == current.ClientId && Revision == current.Revision && CredentialRevision == current.CredentialRevision &&
        ExpiresAt == current.ExpiresAt && current.ObservedAt >= ObservedAt && Redirects.SequenceEqual(current.Redirects, StringComparer.Ordinal);
}
