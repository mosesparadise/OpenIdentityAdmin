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
    internal string Subject => MachineClientRules.Subject(registration.ApplicationId);

    /// <summary>Reads the current finite machine profile and repeats native secret validation under the lock shared with replacement.</summary>
    internal static async ValueTask<MachineGrant?> ReadAsync(ProtocolStorageScope scope, OpenIddictRequest request, CancellationToken token)
    {
        var names = MachineClientRules.ReadScopes(request.Scope);
        if (names is null || !request.IsClientCredentialsGrantType() || request.ClientId is not { } client || request.ClientSecret is not { Length: > 0 } secret)
        {
            return null;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(client, token);
        if (application is null || !await ProtocolStorageRows.ReloadAsync(scope, application, token) || await manager.GetClientIdAsync(application, token) != client ||
            await manager.GetIdAsync(application, token) is not { } id)
        {
            return null;
        }
        var policy = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationPolicy>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ApplicationId == id, current), token);
        if (policy is null || !Eligible(policy, id, scope.Owner.Clock))
        {
            return null;
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        if (!ManagedApplicationProfile.MatchesProfile(MachineClientRules.Profile, descriptor) || descriptor.ClientId != client ||
            names.Any(name => !descriptor.Permissions.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + name)) ||
            !await manager.ValidateClientSecretAsync(application, secret, token))
        {
            return null;
        }
        try
        {
            await ApplicationMutationWriter.RequireMutableAsync(scope, id, client, token);
            var assigned = await ApplicationScopeSelection.ReadAsync(scope, names.ToImmutableArray(), token);
            var current = new ManagedRegistration(id, client, policy.Revision, policy.CredentialRevision, policy.CredentialExpiresAt, policy.ObservedAt, [], MachineClientRules.Profile);
            return new(current, names.ToImmutableArray(), assigned.MachineResources().ToImmutableArray(), assigned);
        }
        catch (ApplicationMutationRejected) { return null; }
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
        policy.Profile == MachineClientRules.Profile && policy.ApplicationId == id && policy.Enabled && !policy.Retired &&
        policy.CredentialRevision > 0 && policy.Revision >= policy.CredentialRevision && policy.CredentialIssuedAt is { } issued &&
        issued >= 0 && issued <= policy.ObservedAt && policy.CredentialExpiresAt is { } expires && expires > ManagedLoginRules.Observe(clock, policy.ObservedAt);
}
