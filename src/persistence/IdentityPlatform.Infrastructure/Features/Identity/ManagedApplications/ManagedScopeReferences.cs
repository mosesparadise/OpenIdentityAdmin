using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Owns exact retained identity sets; neither expiry nor assignment changes release these dependencies.</summary>
internal static class ManagedScopeReferences
{
    /// <summary>Stages one reference per requested catalogue scope alongside an already validated immutable interaction binding.</summary>
    internal static void Add(DbContext database, ManagedLoginInteraction row, ManagedScopeDecision decision)
    {
        RequireBinding(row, decision);
        database.AddRange(Expected(row.SelectorDigest, decision));
    }

    /// <summary>Rejects unexpected tracked or durable references on the currently supported exact OpenID-only public path.</summary>
    internal static async ValueTask<bool> IsOpenIdAsync(DbContext database, ManagedLoginInteraction row, CancellationToken token) =>
        ManagedScopeBinding.IsOpenId(row) && !database.ChangeTracker.Entries<ManagedScopeReference>()
            .Any(entry => entry.Entity.SelectorDigest == row.SelectorDigest) &&
        !await database.Set<ManagedScopeReference>().AsNoTracking().AnyAsync(value => value.SelectorDigest == row.SelectorDigest, token);

    /// <summary>Validates canonical policy and exact retained references, allowing null legacy pairs only with no dependencies.</summary>
    internal static async ValueTask VerifyBoundAsync(DbContext database, ManagedLoginInteraction row, CancellationToken token)
    {
        var decision = ManagedScopeBinding.Read(row);
        if (row.PolicySnapshot is null && row.PolicyDigest is null)
        {
            if (!await IsOpenIdAsync(database, row, token))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return;
        }
        await VerifyAsync(database, row, decision, token);
    }

    /// <summary>Compares complete bounded persisted and tracked reference sets after native saves, detecting omitted, inserted or substituted identities.</summary>
    internal static async ValueTask VerifyAsync(DbContext database, ManagedLoginInteraction row, ManagedScopeDecision decision, CancellationToken token)
    {
        RequireBinding(row, decision);
        var expected = Expected(row.SelectorDigest, decision).Select(Key).Order(StringComparer.Ordinal).ToArray();
        var actual = await database.Set<ManagedScopeReference>().AsNoTracking().Where(value => value.SelectorDigest == row.SelectorDigest)
            .Take(CatalogueRules.MaximumScopes + 1).ToArrayAsync(token);
        var tracked = database.ChangeTracker.Entries<ManagedScopeReference>().Where(entry => entry.Entity.SelectorDigest == row.SelectorDigest).ToArray();
        if (!expected.SequenceEqual(actual.Select(Key).Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            tracked.Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted ||
                !expected.Contains(Key(entry.Entity), StringComparer.Ordinal)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Requires exact interaction ownership and canonical policy before either staging or validating its retained dependencies.</summary>
    private static void RequireBinding(ManagedLoginInteraction row, ManagedScopeDecision decision)
    {
        if (row.PolicySnapshot != ManagedScopeCodec.Encode(decision) || row.PolicyDigest != ManagedLoginRules.Digest(row.PolicySnapshot) ||
            row.ApplicationId != decision.ApplicationId || row.ApplicationRevision != decision.ApplicationRevision)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Produces immutable identity membership from the canonical decision, preserving the original audience even after scope remapping.</summary>
    private static ManagedScopeReference[] Expected(string selector, ManagedScopeDecision decision) => decision.Scopes.Select(scope =>
        new ManagedScopeReference { SelectorDigest = selector, ScopeId = scope.Id, AudienceId = scope.Kind == CatalogueRules.Api ? decision.Audience!.Id : null }).ToArray();

    /// <summary>Uses length-safe structured encoding rather than ambiguous concatenation for exact membership comparison.</summary>
    private static string Key(ManagedScopeReference value) => System.Text.Json.JsonSerializer.Serialize(new[] { value.SelectorDigest, value.ScopeId, value.AudienceId });
}
