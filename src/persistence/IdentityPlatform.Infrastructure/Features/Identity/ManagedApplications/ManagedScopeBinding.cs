using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Stages immutable policy and verifies protected transports against current native authority and retained references.</summary>
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

    /// <summary>Reads canonical bound policy; legacy rows represent only the original fixed OpenID permission profile.</summary>
    internal static ManagedScopeDecision Read(ManagedLoginInteraction row)
    {
        var decision = row.PolicySnapshot is null && row.PolicyDigest is null
            ? OpenId(row.ApplicationId, row.ApplicationRevision)
            : ManagedScopeCodec.Decode(row.PolicySnapshot!, row.PolicyDigest!);
        if (decision.ApplicationId != row.ApplicationId || decision.ApplicationRevision != row.ApplicationRevision)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return decision;
    }

    /// <summary>Binds every new protected transport; absent historic fields are restricted to exact OpenID-only interactions.</summary>
    internal static bool MatchesDigest(ManagedLoginInteraction row, string? digest) =>
        digest == ManagedLoginRules.Digest(ManagedScopeCodec.Encode(Read(row))) || digest is null && IsOpenId(row);

    /// <summary>Requires canonical retained dependencies and independently recaptured current native/catalogue authority.</summary>
    internal static async ValueTask RequireAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, string client, CancellationToken token)
    {
        await scope.Owner.RunAsync(current => ManagedScopeReferences.VerifyBoundAsync(scope.Owner.Database, row, current), token);
        var decision = Read(row);
        if (!decision.Requested.SequenceEqual(decision.Granted, StringComparer.Ordinal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ManagedScopePolicy.VerifyAsync(scope, client, decision, token);
    }

    /// <summary>Constructs the original fixed policy for legacy compatibility without inferring consent.</summary>
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
