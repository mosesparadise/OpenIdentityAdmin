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
        if (policy is null || policy.ApplicationId != id || policy.Profile != ApplicationEditorRules.Profile || !ManagedApplicationProfile.Matches(descriptor))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        return new(policy, new ApplicationWriteState(application, descriptor, policy, null));
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
internal sealed class GrantTargetObservation(ApplicationPolicy policy, ApplicationWriteState expected)
{
    internal ApplicationPolicy Policy => policy;

    /// <summary>Requires unchanged native configuration, credential and policy through actual independent reloads after save callbacks.</summary>
    internal Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => expected.VerifyAsync(scope, token);
}
