using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Stages immutable policy on new interactions while keeping every current public path restricted to the original OpenID profile.</summary>
internal static class ManagedScopeBinding
{
    /// <summary>Checks the actual retained set as well as the stored representation before current public protocol work proceeds.</summary>
    internal static async ValueTask RequireOpenIdAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, CancellationToken token)
    {
        if (!await scope.Owner.RunAsync(current => ManagedScopeReferences.IsOpenIdAsync(scope.Owner.Database, row, current), token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Constructs the sole currently issuable policy; fixed native permissions remain derived from the supported profile.</summary>
    internal static ManagedScopeDecision OpenId(string applicationId, long revision) => new(ManagedScopeCodec.FormatVersion, applicationId, revision,
        ManagedApplicationProfile.OpenIdPermissionsDigest, [OpenIddictConstants.Scopes.OpenId], [OpenIddictConstants.Scopes.OpenId], [], null);

    /// <summary>Copies only validated immutable policy into a previously unbound reservation; it never infers consent or persists changes itself.</summary>
    internal static void Attach(ManagedLoginInteraction row, ManagedScopeDecision decision)
    {
        if (row.PolicySnapshot is not null || row.PolicyDigest is not null || row.ApplicationId != decision.ApplicationId ||
            row.ApplicationRevision != decision.ApplicationRevision)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        row.PolicySnapshot = ManagedScopeCodec.Encode(decision);
        row.PolicyDigest = ManagedLoginRules.Digest(row.PolicySnapshot);
    }

    /// <summary>Allows both-null historical rows or exactly the current OpenID policy; malformed or extended values never fall back to legacy.</summary>
    internal static bool IsOpenId(ManagedLoginInteraction row)
    {
        if (row.PolicySnapshot is null || row.PolicyDigest is null)
        {
            return row.PolicySnapshot is null && row.PolicyDigest is null;
        }
        var expected = ManagedScopeCodec.Encode(OpenId(row.ApplicationId, row.ApplicationRevision));
        return row.PolicySnapshot == expected && row.PolicyDigest == ManagedLoginRules.Digest(expected);
    }
}
