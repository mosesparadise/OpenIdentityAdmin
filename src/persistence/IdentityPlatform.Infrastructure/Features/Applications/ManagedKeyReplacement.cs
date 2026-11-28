using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stages a public-key replacement in the caller's owned governance transaction and verifies the resulting native registration.</summary>
internal static class ManagedKeyReplacement
{
    /// <summary>Requires a matching application version, the existing locally approved native key and a future successor expiry.</summary>
    internal static async ValueTask<ApplicationWriteState> StageAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, CancellationToken token)
    {
        var id = command.ApplicationId ?? throw ProtocolStorageOwner.Unavailable();
        var replacement = command.PublicKey ?? throw ProtocolStorageOwner.Unavailable();
        var expires = command.CredentialExpiry ?? throw ProtocolStorageOwner.Unavailable();
        await ManagedRegistration.LockAsync(scope, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = await manager.FindByIdAsync(id, token);
        if (app is null || !await ProtocolStorageRows.ReloadAsync(scope, app, token) || await manager.GetIdAsync(app, token) != id)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, app, token);
        await ApplicationMutationWriter.RequireMutableAsync(scope, id, descriptor.ClientId!, token);
        var (policy, approval) = await LoadAsync(scope, id, token);
        if (policy.Retired || !ManagedApplicationProfile.MatchesKeyProfile(policy.Profile, descriptor) ||
            AdministratorPublicKey.FromNative(descriptor.JsonWebKeySet) is not { } previous || previous.Fingerprint != approval.Fingerprint ||
            approval.CredentialRevision != policy.CredentialRevision || approval.Thumbprint != ApplicationKeyApproval.Material(previous))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        if (!ApplicationVersion.Matches(command.Version, id, policy.Revision))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
        }
        Advance(scope, policy, approval, replacement, expires);
        var keys = new JsonWebKeySet();
        keys.Keys.Add(replacement.CreateNativeKey());
        descriptor.JsonWebKeySet = keys;
        var expected = new ApplicationWriteState(app, descriptor, policy, null, approvedKey: replacement);
        await manager.UpdateAsync(app, descriptor, token);
        await expected.VerifyAsync(scope, token);
        return expected;
    }

    /// <summary>Reads the tracked application and its single approved key under the governance lock.</summary>
    private static ValueTask<(ApplicationPolicy, ApplicationKeyApproval)> LoadAsync(ProtocolStorageScope scope, string id, CancellationToken token) =>
        scope.Owner.RunAsync<(ApplicationPolicy, ApplicationKeyApproval)>(async current =>
        {
            var policy = await scope.Owner.Database.Set<ApplicationPolicy>().SingleOrDefaultAsync(row => row.ApplicationId == id, current);
            var approval = await scope.Owner.Database.Set<ApplicationKeyApproval>().SingleOrDefaultAsync(row => row.ApplicationId == id, current);
            return policy is not null && approval is not null ? (policy, approval) : throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }, token);

    /// <summary>Advances application and credential versions together, preserving consumed assertion IDs and every existing permission.</summary>
    private static void Advance(ProtocolStorageScope scope, ApplicationPolicy policy, ApplicationKeyApproval approval,
        AdministratorPublicKey replacement, DateTimeOffset expires)
    {
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (now < policy.ObservedAt || expires.ToUnixTimeSeconds() <= now)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        if (approval.Thumbprint == ApplicationKeyApproval.Material(replacement))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
        policy.Revision = checked(policy.Revision + 1);
        policy.CredentialRevision = checked(policy.CredentialRevision + 1);
        policy.ObservedAt = now;
        policy.CredentialIssuedAt = now;
        policy.CredentialExpiresAt = expires.ToUnixTimeSeconds();
        approval.Replace(policy, replacement);
    }
}
