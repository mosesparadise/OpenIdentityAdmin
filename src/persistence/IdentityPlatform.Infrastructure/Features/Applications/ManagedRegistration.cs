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

    /// <summary>Retains exact OpenID-only issuer eligibility independently of the catalogue policy reader.</summary>
    internal static async ValueTask<ManagedRegistration?> ReadAsync(ProtocolStorageScope scope, string clientId, CancellationToken token)
    {
        var current = await ManagedRegistrationState.ReadAsync(scope, clientId, token);
        return current is { IsOpenId: true } ? current.Registration : null;
    }

    /// <summary>Persists the original registration watermark through the same current-state authority used by native reads.</summary>
    internal ValueTask<ManagedRegistration> ObserveAsync(ProtocolStorageScope scope, CancellationToken token) =>
        ManagedRegistrationState.ObserveAsync(scope, this, token);

    /// <summary>Checks immutable registration authority without record-array reference equality or logging native credentials.</summary>
    internal bool Matches(ManagedRegistration? current) => current is not null && ApplicationId == current.ApplicationId &&
        ClientId == current.ClientId && Revision == current.Revision && CredentialRevision == current.CredentialRevision &&
        ExpiresAt == current.ExpiresAt && current.ObservedAt >= ObservedAt && Redirects.SequenceEqual(current.Redirects, StringComparer.Ordinal);
}
