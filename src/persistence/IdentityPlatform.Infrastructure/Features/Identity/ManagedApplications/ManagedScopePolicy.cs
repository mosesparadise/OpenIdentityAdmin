using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Captures current native permissions and catalogue metadata under governance without granting issuer eligibility or consent.</summary>
internal static class ManagedScopePolicy
{
    /// <summary>Freezes bounded caller input before awaiting locks and resolves only exact currently assigned, enabled managed scopes.</summary>
    internal static async ValueTask<ManagedScopeDecision> CaptureAsync(ProtocolStorageScope scope, string clientId,
        IReadOnlyCollection<string> requested, IReadOnlyCollection<string> granted, CancellationToken token)
        => await TryCaptureAsync(scope, clientId, requested, granted, token) ?? throw ProtocolStorageOwner.Unavailable();

    /// <summary>Returns no authority for ordinary missing or disabled policy without converting storage failures into protocol denials.</summary>
    internal static async ValueTask<ManagedScopeDecision?> TryCaptureAsync(ProtocolStorageScope scope, string clientId,
        IReadOnlyCollection<string> requested, IReadOnlyCollection<string> granted, CancellationToken token)
    {
        var request = Freeze(requested);
        var grant = Freeze(granted);
        if (grant.Except(request, StringComparer.Ordinal).Any())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ManagedRegistration.LockAsync(scope, token);
        var application = await ManagedRegistrationState.ReadAsync(scope, clientId, token);
        if (application is null)
        {
            return null;
        }
        try
        {
            var (entries, audience) = await ResolveAsync(scope, application.Permissions, request, token);
            var decision = new ManagedScopeDecision(ManagedScopeCodec.FormatVersion, application.Registration.ApplicationId,
                application.Registration.Revision, FactorBinding.Digest(application.Permissions.ToArray()), request, grant, entries, audience);
            _ = ManagedScopeCodec.Encode(decision);
            return decision;
        }
        catch (CatalogueRejected) { return null; }
    }

    /// <summary>Reconstructs authority from current native/product state; a stored snapshot never authorizes its own continued use.</summary>
    internal static async ValueTask VerifyAsync(ProtocolStorageScope scope, string clientId, ManagedScopeDecision decision, CancellationToken token)
    {
        var expected = ManagedScopeCodec.Encode(decision);
        var actual = await CaptureAsync(scope, clientId, decision.Requested, decision.Granted, token);
        if (expected != ManagedScopeCodec.Encode(actual))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Distinguishes current policy invalidation from infrastructure faults for a bounded bearer-token denial.</summary>
    internal static async ValueTask<bool> TryVerifyAsync(ProtocolStorageScope scope, string clientId, ManagedScopeDecision decision, CancellationToken token)
    {
        var expected = ManagedScopeCodec.Encode(decision);
        var actual = await TryCaptureAsync(scope, clientId, decision.Requested, decision.Granted, token);
        return actual is not null && expected == ManagedScopeCodec.Encode(actual);
    }

    /// <summary>Rejects duplicate or malformed members before sorting, preserving ordinal semantics and a fixed input bound.</summary>
    private static ImmutableArray<string> Freeze(IReadOnlyCollection<string> values)
    {
        if (values.Count is <= 0 or > CatalogueRules.MaximumScopes + 1)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var result = values.Order(StringComparer.Ordinal).ToImmutableArray();
        return ManagedScopeCodec.Names(result) ? result : throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Binds every requested scope and its original audience, including scopes not ultimately granted, within the finite request limit.</summary>
    private static async ValueTask<(ImmutableArray<ManagedScopeEntry>, ManagedScopeAudience?)> ResolveAsync(ProtocolStorageScope scope,
        ImmutableArray<string> permissions, ImmutableArray<string> requested, CancellationToken token)
    {
        var entries = ImmutableArray.CreateBuilder<ManagedScopeEntry>();
        ManagedScopeAudience? audience = null;
        foreach (var name in requested.Where(value => value != OpenIddictConstants.Scopes.OpenId))
        {
            if (!permissions.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + name, StringComparer.Ordinal))
            {
                throw new CatalogueRejected(CatalogueFailure.Unsupported);
            }
            var policy = await ReadScopeAsync(scope, name, token);
            if (policy.AudienceId is not null)
            {
                var current = await CatalogueAudience.ReadAsync(scope, policy.AudienceId, token);
                var captured = new ManagedScopeAudience(current.Id, current.Resource, current.Revision, FactorBinding.Digest(current));
                if (!current.Enabled || current.Resource != policy.Resource || audience is not null && audience != captured)
                {
                    throw new CatalogueRejected(CatalogueFailure.Unsupported);
                }
                audience = captured;
            }
            entries.Add(new(policy.Id, policy.Name, policy.Revision, policy.Kind, policy.Disclosure, FactorBinding.Digest(policy)));
        }
        return (entries.ToImmutable(), audience);
    }

    /// <summary>Resolves by name then reloads by native identity, rejecting aliases, system identities, disabled rows and cache drift.</summary>
    private static async ValueTask<CatalogueScope> ReadScopeAsync(ProtocolStorageScope scope, string name, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.FindByNameAsync(name, token) ?? throw new CatalogueRejected(CatalogueFailure.Unsupported);
        var id = await manager.GetIdAsync(native, token) ?? throw new CatalogueRejected(CatalogueFailure.Unsupported);
        var current = await CatalogueScope.ReadAsync(scope, id, token);
        return current.Name == name && current.Enabled ? current : throw new CatalogueRejected(CatalogueFailure.Unsupported);
    }
}
