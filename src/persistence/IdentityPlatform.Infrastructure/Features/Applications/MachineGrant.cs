using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes the library-authenticated machine registration, assigned API scopes and live catalogue resources inside governance ownership.</summary>
internal sealed class MachineGrant(ManagedRegistration registration, ImmutableArray<string> scopes, ImmutableArray<string> resources, ApplicationScopeSelection selection)
{
    internal ManagedRegistration Registration => registration;
    internal ImmutableArray<string> Scopes => scopes;
    internal ImmutableArray<string> Resources => resources;
    internal MachineTokenReferences References => selection.MachineReferences;
    internal string Subject => MachineClientRules.Subject(registration.ApplicationId);

    /// <summary>Reads the current finite machine profile and repeats native secret validation under the lock shared with replacement.</summary>
    internal static async ValueTask<(MachineGrant? Grant, string? Error)> ReadAsync(ProtocolStorageScope scope, OpenIddictRequest request, CancellationToken token, ApplicationAssertionReceipt? assertion = null)
    {
        var names = MachineClientRules.ReadScopes(request.Scope);
        if (names is null)
        {
            return (null, OpenIddictConstants.Errors.InvalidScope);
        }
        if (!request.IsClientCredentialsGrantType() || request.ClientId is not { } client)
        {
            return (null, OpenIddictConstants.Errors.InvalidClient);
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(client, token);
        if (application is null || !await ProtocolStorageRows.ReloadAsync(scope, application, token) || await manager.GetClientIdAsync(application, token) != client ||
            await manager.GetIdAsync(application, token) is not { } id)
        {
            return (null, OpenIddictConstants.Errors.InvalidClient);
        }
        var policy = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationPolicy>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ApplicationId == id, current), token);
        if (policy is null || !Eligible(policy, id, scope.Owner.Clock))
        {
            return (null, OpenIddictConstants.Errors.InvalidClient);
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        if (descriptor.ClientId != client || !await AuthenticatedAsync(scope, policy, descriptor, manager, application, request, assertion, token))
        {
            return (null, OpenIddictConstants.Errors.InvalidClient);
        }
        if (names.Any(name => !descriptor.Permissions.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + name)))
        {
            return (null, OpenIddictConstants.Errors.InvalidScope);
        }
        try
        {
            await ApplicationMutationWriter.RequireMutableAsync(scope, id, client, token);
            var assigned = await ApplicationScopeSelection.ReadAsync(scope, names.ToImmutableArray(), token);
            var current = new ManagedRegistration(id, client, policy.Revision, policy.CredentialRevision, policy.CredentialExpiresAt, policy.ObservedAt, [], policy.Profile);
            var resources = assigned.MachineResources();
            return resources.Length == 1 ? (new(current, names.ToImmutableArray(), resources.ToImmutableArray(), assigned), null) :
                (null, OpenIddictConstants.Errors.InvalidScope);
        }
        catch (ApplicationMutationRejected) { return (null, OpenIddictConstants.Errors.InvalidScope); }
    }

    /// <summary>Checks the current native credential and its approved material using the registration's sole authentication method.</summary>
    private static async ValueTask<bool> AuthenticatedAsync(ProtocolStorageScope scope, ApplicationPolicy policy, OpenIddictApplicationDescriptor descriptor,
        IOpenIddictApplicationManager manager, object application, OpenIddictRequest request, ApplicationAssertionReceipt? assertion, CancellationToken token)
    {
        if (policy.Profile == MachineClientRules.Profile)
        {
            return assertion is null && request.ClientAssertion is null && request.ClientSecret is { Length: > 0 } secret &&
                ManagedApplicationProfile.MatchesProfile(policy.Profile, descriptor) && await manager.ValidateClientSecretAsync(application, secret, token);
        }
        if (assertion is null || request.ClientAssertion is null || request.ClientSecret is not null || assertion.ApplicationId != policy.ApplicationId ||
            assertion.Revision != policy.Revision || assertion.CredentialRevision != policy.CredentialRevision || assertion.CredentialExpiresAt != policy.CredentialExpiresAt ||
            !assertion.Allows(scope.Owner.Clock, policy.ObservedAt) || !ManagedApplicationProfile.MatchesKeyProfile(policy.Profile, descriptor) ||
            AdministratorPublicKey.FromNative(descriptor.JsonWebKeySet) is not { } key || key.Fingerprint != assertion.Fingerprint)
        {
            return false;
        }
        var approval = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationKeyApproval>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ApplicationId == policy.ApplicationId, current), token);
        return approval?.Matches(policy, key, key.KeyId, ApplicationKeyApproval.Material(key), ManagedLoginRules.Observe(scope.Owner.Clock, policy.ObservedAt)) == true;
    }

    /// <summary>Persists a successful machine exchange's time under the same policy and credential revision as its native token.</summary>
    internal async ValueTask ObserveAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, registration.ObservedAt);
        var changed = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<ApplicationPolicy>()
            .Where(row => row.ApplicationId == registration.ApplicationId && row.Profile == registration.Profile && row.Enabled && !row.Retired &&
                row.Revision == registration.Revision && row.CredentialRevision == registration.CredentialRevision &&
                row.ObservedAt >= registration.ObservedAt && row.ObservedAt <= now && row.CredentialExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ObservedAt, now), current)), token, write: true);
        if (changed != 1)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rejects changed catalogue identities, audience revisions or registration authority across endpoint handoff and response formatting.</summary>
    internal async ValueTask<bool> MatchesAsync(ProtocolStorageScope scope, MachineGrant? other, CancellationToken token)
    {
        if (other is null || !registration.Matches(other.Registration) || !scopes.SequenceEqual(other.Scopes, StringComparer.Ordinal) ||
            !resources.SequenceEqual(other.Resources, StringComparer.Ordinal))
        {
            return false;
        }
        await selection.VerifyAsync(scope, token);
        return true;
    }

    /// <summary>Requires a current confidential machine credential and terminal-state exclusion independently of native authentication.</summary>
    private static bool Eligible(ApplicationPolicy policy, string id, TimeProvider clock) =>
        policy.Profile is MachineClientRules.Profile or ApplicationKeyApproval.MachineProfile && policy.ApplicationId == id && policy.Enabled && !policy.Retired &&
        policy.CredentialRevision > 0 && policy.Revision >= policy.CredentialRevision && policy.CredentialIssuedAt is { } issued &&
        issued >= 0 && issued <= policy.ObservedAt && policy.CredentialExpiresAt is { } expires && expires > ManagedLoginRules.Observe(clock, policy.ObservedAt);
}
