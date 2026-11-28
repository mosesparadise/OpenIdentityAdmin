using System.Collections.Immutable;
using System.Security.Cryptography;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stages one native aggregate and finite audit in the caller's owned transaction; this helper cannot commit or publish a response.</summary>
internal static class ApplicationMutationWriter
{
    private const int SecretBytes = 32;
    private const string PilotClient = "identityplatform-pilot";
    /// <summary>Uses real managers for all native writes and leaves version/policy persistence inside the same original transaction.</summary>
    internal static async Task<ApplicationWriteState> StageAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        if (command.Kind is ApplicationMutationKind.Create or ApplicationMutationKind.CreateNative or ApplicationMutationKind.CreateBrowser or ApplicationMutationKind.CreateMachine)
        {
            return await ApplicationCreation.StageAsync(scope, manager, command, null, token);
        }
        var application = await manager.FindByIdAsync(command.ApplicationId!, token)
            ?? throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        if (!await ProtocolStorageRows.ReloadAsync(scope, application, token) || await manager.GetIdAsync(application, token) != command.ApplicationId)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        await RequireMutableAsync(scope, command.ApplicationId!, descriptor.ClientId!, token);
        var policy = await scope.Owner.RunAsync(async current => await ((PlatformDbContext)scope.Owner.Database).Set<ApplicationPolicy>()
            .SingleOrDefaultAsync(row => row.ApplicationId == command.ApplicationId, current), token);
        if (policy is null || policy.ApplicationId != command.ApplicationId || !ManagedApplicationProfile.MatchesProfile(policy.Profile, descriptor))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        if ((policy.Profile is ApplicationEditorRules.NativeProfile or ApplicationEditorRules.BrowserProfile &&
            command.Kind is ApplicationMutationKind.Replace or ApplicationMutationKind.Clone) ||
            (policy.Profile == ApplicationEditorRules.NativeProfile && command.Kind == ApplicationMutationKind.Edit && !command.Redirects.All(ApplicationEditorRules.IsNativeRedirect)))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        if (policy.Profile == MachineClientRules.Profile && command.Kind == ApplicationMutationKind.Clone ||
            command.Kind == ApplicationMutationKind.Edit && (policy.Profile == MachineClientRules.Profile) != command.Redirects.IsEmpty)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        if (!ApplicationVersion.Matches(command.Version, policy.ApplicationId, policy.Revision))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
        }
        if (policy.Retired)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
        if (command.Kind == ApplicationMutationKind.Clone)
        {
            var source = await ApplicationCloneSource.ReadAsync(scope, application, descriptor, policy, command, token);
            return await ApplicationCreation.StageAsync(scope, manager, command, source, token);
        }
        var origins = await ApplicationBrowserOrigin.SnapshotAsync(scope, policy, token);
        return await UpdateAsync(scope, manager, application, descriptor, policy, command, origins, token);
    }

    /// <summary>Advances one aggregate revision; only explicit replacement changes the secret and credential lifecycle.</summary>
    private static async Task<ApplicationWriteState> UpdateAsync(ProtocolStorageScope scope, IOpenIddictApplicationManager manager, object application,
        OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, ApplicationMutationCommand command, ImmutableArray<string> origins, CancellationToken token)
    {
        var now = Now(scope, command);
        if (now < policy.ObservedAt)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        if (command.Kind is ApplicationMutationKind.Disable or ApplicationMutationKind.Enable)
        {
            if (command.Kind == ApplicationMutationKind.Enable && await scope.Owner.RunAsync(async current =>
                await scope.Owner.Database.Set<ApplicationGrantJob>().AsNoTracking()
                    .AnyAsync(row => row.ApplicationId == policy.ApplicationId && row.State != GrantJobState.Succeeded, current), token))
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
            }
            return ChangeLifecycle(application, descriptor, policy, command, now, origins);
        }
        if (command.Kind == ApplicationMutationKind.AssignScopes)
        {
            return await AssignScopesAsync(scope, manager, application, descriptor, policy, command, now, origins, token);
        }
        string? secret = null;
        policy.Revision = checked(policy.Revision + 1);
        policy.ObservedAt = now;
        if (command.Kind == ApplicationMutationKind.Replace)
        {
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
            descriptor.ClientSecret = secret;
            policy.CredentialRevision = checked(policy.CredentialRevision + 1);
            policy.CredentialIssuedAt = now;
            policy.CredentialExpiresAt = command.CredentialExpiry!.Value.ToUnixTimeSeconds();
        }
        else if (command.Kind == ApplicationMutationKind.Edit)
        {
            descriptor.DisplayName = command.DisplayName;
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.UnionWith(command.Redirects.Select(value => new Uri(value)));
        }
        else
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var expected = new ApplicationWriteState(application, descriptor, policy, secret, origins: origins);
        await manager.UpdateAsync(application, descriptor, token);
        return expected;
    }

    /// <summary>Replaces only additional scope permissions after resolving current catalogue eligibility under the existing governance transaction.</summary>
    private static async Task<ApplicationWriteState> AssignScopesAsync(ProtocolStorageScope scope, IOpenIddictApplicationManager manager, object application,
        OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, ApplicationMutationCommand command, long now, ImmutableArray<string> origins, CancellationToken token)
    {
        var selection = await ApplicationScopeSelection.ReadAsync(scope, command.Scopes, token);
        if (policy.Profile == MachineClientRules.Profile)
        {
            _ = selection.MachineResources();
        }
        var prefix = OpenIddictConstants.Permissions.Prefixes.Scope;
        descriptor.Permissions.RemoveWhere(value => value.StartsWith(prefix, StringComparison.Ordinal));
        descriptor.Permissions.UnionWith(command.Scopes.Select(name => prefix + name));
        policy.Revision = checked(policy.Revision + 1);
        policy.ObservedAt = now;
        var expected = new ApplicationWriteState(application, descriptor, policy, null, selection, origins: origins);
        await manager.UpdateAsync(application, descriptor, token);
        return expected;
    }

    /// <summary>Advances product authority without touching any native credential/configuration; same-state requests never create audit noise.</summary>
    private static ApplicationWriteState ChangeLifecycle(object application, OpenIddictApplicationDescriptor descriptor,
        ApplicationPolicy policy, ApplicationMutationCommand command, long now, ImmutableArray<string> origins)
    {
        var enabled = command.Kind == ApplicationMutationKind.Enable;
        if (command.Reason is not { } reason || !Enum.IsDefined(reason))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (policy.Enabled == enabled)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
        }
        policy.Enabled = enabled;
        policy.Revision = checked(policy.Revision + 1);
        policy.ObservedAt = now;
        return new(application, descriptor, policy, null, origins: origins);
    }

    /// <summary>Rechecks the exact operator-provided expiry under locks; no default or retry can silently extend it.</summary>
    internal static long Now(ProtocolStorageScope scope, ApplicationMutationCommand command)
    {
        var now = scope.Owner.Clock.GetUtcNow();
        if (command.CredentialExpiry is { } expiry && expiry <= now)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        return now.ToUnixTimeSeconds();
    }

    /// <summary>Protects both fixed client names and persisted system application identities even when native names have drifted.</summary>
    internal static async Task RequireMutableAsync(ProtocolStorageScope scope, string id, string clientId, CancellationToken token)
    {
        if (Reserved(clientId))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
        }
        var bound = await scope.Owner.RunAsync(async current =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            return await database.Set<AdministratorProtocolProfile>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, current) ||
                await database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, current);
        }, token);
        if (bound)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
        }
    }

    /// <summary>Rejects reserved or existing destination identities without adopting their native state.</summary>
    internal static async Task RequireNewIdentityAsync(IOpenIddictApplicationManager manager, string clientId, CancellationToken token)
    {
        if (Reserved(clientId))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
        }
        if (await manager.FindByClientIdAsync(clientId, token) is not null)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
        }
    }

    /// <summary>Rejects case aliases of reserved registrations independently of provider collation.</summary>
    private static bool Reserved(string clientId) => string.Equals(clientId, AdministratorProtocolIdentity.ClientId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(clientId, PilotClient, StringComparison.OrdinalIgnoreCase);


}

/// <summary>Freezes the nonsecret audit identity and time for exact post-save comparison.</summary>
internal sealed record ApplicationAuditStamp(Guid Id, Guid Correlation, long At);
