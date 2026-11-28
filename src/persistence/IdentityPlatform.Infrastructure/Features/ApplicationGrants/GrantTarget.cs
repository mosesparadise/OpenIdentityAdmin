using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Revalidates cleanup eligibility independently of issuer admission, which intentionally rejects disabled and expired registrations.</summary>
internal static class GrantTarget
{
    /// <summary>Requires an exact native/product identity and non-system profile; credential expiry never prevents revocation.</summary>
    internal static async Task<GrantTargetObservation> ReadAsync(ProtocolStorageScope scope, string id, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByIdAsync(id, token);
        if (application is null || !await ProtocolStorageRows.ReloadAsync(scope, application, token) || await manager.GetIdAsync(application, token) != id)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        await ApplicationMutationWriter.RequireMutableAsync(scope, id, descriptor.ClientId!, token);
        var policy = await scope.Owner.RunAsync(async current => await ((PlatformDbContext)scope.Owner.Database).Set<ApplicationPolicy>()
            .AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == id, current), token);
        if (policy is null || policy.ApplicationId != id || !ManagedApplicationProfile.MatchesProfile(policy.Profile, descriptor) &&
            !await ApplicationKeyApproval.MatchesStoredAsync(scope, policy, descriptor, token))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        var retirement = policy.Retired ? await ApplicationRetirementBinding.ReadAsync(scope, policy, token) : null;
        return new(policy, application, descriptor, retirement, await ApplicationBrowserOrigin.SnapshotAsync(scope, policy, token));
    }

    /// <summary>Checks the exact original precondition before state conflict, without treating credential age as job authority.</summary>
    internal static void RequireDisabled(ApplicationPolicy policy, string? version = null)
    {
        if (version is not null && !ApplicationVersion.Matches(version, policy.ApplicationId, policy.Revision))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
        }
        if (policy.Enabled)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
    }
}

/// <summary>Retains a detached policy and privately frozen native/credential expectations for the original target.</summary>
internal sealed class GrantTargetObservation(ApplicationPolicy policy, object application, OpenIddictApplicationDescriptor descriptor,
    ApplicationRetirementBinding? retirement, ImmutableArray<string> origins = default)
{
    private readonly ApplicationWriteState expected = new(application, descriptor, policy, null, origins: origins);
    internal ApplicationPolicy Policy => policy;
    internal ApplicationRetirementBinding? Retirement => retirement;

    /// <summary>Requires unchanged native configuration, credential and policy through actual independent reloads after save callbacks.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        await expected.VerifyAsync(scope, token);
        if (retirement is not null)
        {
            await retirement.VerifyAsync(scope, token);
        }
    }

    /// <summary>Restricts terminal targets to their original linked job, excluding new Submit and unrelated historical continuations.</summary>
    internal void RequireJob(Guid? id)
    {
        if (policy.Retired && (retirement is null || retirement.Receipt.GrantJobId != id))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
    }

    /// <summary>Freezes the intended terminal policy against the original private native descriptor; the owning transaction must attach the policy first.</summary>
    internal ApplicationWriteState Retire(long now)
    {
        if (policy.Retired)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
        if (now < policy.ObservedAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        policy.Retired = true;
        policy.Enabled = false;
        policy.Revision = checked(policy.Revision + 1);
        policy.ObservedAt = now;
        return new(application, descriptor, policy, null, origins: origins);
    }
}
