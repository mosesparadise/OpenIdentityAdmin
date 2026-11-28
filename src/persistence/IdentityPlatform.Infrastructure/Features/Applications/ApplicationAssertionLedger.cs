using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Consumes an already signature-validated managed assertion under the serial lock used for credential replacement and retirement.</summary>
internal static class ApplicationAssertionLedger
{
    /// <summary>Commits a one-use receipt only for the actual current native key and the same approved revision.</summary>
    internal static async ValueTask<ApplicationAssertionReceipt?> ConsumeAsync(ProtocolStorageScope scope, SignedClientAssertion assertion, CancellationToken token, Action<ProtocolStorageOwner>? opened = null)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        opened?.Invoke(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = await manager.FindByClientIdAsync(assertion.ClientId, token);
        if (app is null || !await ProtocolStorageRows.ReloadAsync(scope, app, token) || await manager.GetClientIdAsync(app, token) != assertion.ClientId ||
            await manager.GetIdAsync(app, token) is not { } id || await manager.GetClientTypeAsync(app, token) != OpenIddictConstants.ClientTypes.Confidential)
        {
            return null;
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, app, token);
        if (AdministratorPublicKey.FromNative(descriptor.JsonWebKeySet) is not { } key)
        {
            return null;
        }
        try
        {
            await ApplicationMutationWriter.RequireMutableAsync(scope, id, assertion.ClientId, token);
        }
        catch (ApplicationMutationRejected error) when (error.Failure == ApplicationMutationFailure.Forbidden) { return null; }
        var receipt = await ReserveAsync(scope, id, descriptor, key, assertion, token);
        if (receipt is null)
        {
            return null;
        }
        await transaction.CommitAsync(token);
        return receipt;
    }

    /// <summary>Checks retained duplicates across rotations, advances the application time watermark and saves the unique receipt atomically.</summary>
    private static ValueTask<ApplicationAssertionReceipt?> ReserveAsync(ProtocolStorageScope scope, string id, OpenIddictApplicationDescriptor descriptor, AdministratorPublicKey key, SignedClientAssertion proof, CancellationToken token) =>
        scope.Owner.RunAsync<ApplicationAssertionReceipt?>(async current =>
        {
            var db = scope.Owner.Database;
            var policy = await db.Set<ApplicationPolicy>().SingleOrDefaultAsync(row => row.ApplicationId == id, current);
            var approval = await db.Set<ApplicationKeyApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == id, current);
            if (policy is null || approval is null || !ManagedApplicationProfile.MatchesKeyProfile(policy.Profile, descriptor) ||
                policy.Revision < policy.CredentialRevision || policy.CredentialIssuedAt > policy.ObservedAt)
            {
                return null;
            }
            var now = ManagedLoginRules.Observe(scope.Owner.Clock, policy.ObservedAt);
            var use = ApplicationAssertionUse.FromProof(policy, approval, key, proof, now);
            if (use is null)
            {
                return null;
            }
            var ledger = db.Set<ApplicationAssertionUse>();
            await CleanAsync(db, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), current);
            if (await ledger.AnyAsync(row => row.ApplicationId == id && row.Digest == use.Digest, current) ||
                await ledger.LongCountAsync(current) >= ManagedLoginRules.RetainedLimit)
            {
                return null;
            }
            policy.ObservedAt = now;
            ledger.Add(use);
            await db.SaveChangesAsync(current);
            var finished = ManagedLoginRules.Observe(scope.Owner.Clock, now);
            return ClientAssertionLimits.Allows(proof.Issued, proof.Expires, proof.NotBefore, finished) &&
                approval.Matches(policy, key, proof.KeyId, proof.KeyThumbprint, finished) && await ledger.AsNoTracking().AnyAsync(row =>
                    row.ApplicationId == id && row.Digest == use.Digest && row.CredentialRevision == use.CredentialRevision && row.RetainUntil == use.RetainUntil, current)
                ? new(id, policy.Revision, approval.CredentialRevision, approval.Fingerprint, finished, proof.Expires, policy.CredentialExpiresAt!.Value) : null;
        }, token, write: true);

    /// <summary>Bounds global retention and advances each affected client's time watermark before removing spent identifiers.</summary>
    private static async Task CleanAsync(DbContext db, long now, CancellationToken token)
    {
        var ledger = db.Set<ApplicationAssertionUse>();
        var expired = await ledger.Where(row => row.RetainUntil < now).OrderBy(row => row.RetainUntil)
            .ThenBy(row => row.ApplicationId).ThenBy(row => row.Digest).Take(ManagedLoginRules.CleanupLimit).ToArrayAsync(token);
        if (expired.Length == 0)
        {
            return;
        }
        var ids = expired.Select(row => row.ApplicationId).Distinct(StringComparer.Ordinal).ToArray();
        await db.Set<ApplicationPolicy>().Where(row => ids.Contains(row.ApplicationId) && row.ObservedAt < now)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ObservedAt, now), token);
        ledger.RemoveRange(expired);
        await db.SaveChangesAsync(token);
    }
}

/// <summary>Freezes the permanent registration, credential and original proof deadline at a committed assertion consumption.</summary>
internal sealed record ApplicationAssertionReceipt(string ApplicationId, long Revision, long CredentialRevision, string Fingerprint, long ObservedAt,
    long AssertionExpiresAt, long CredentialExpiresAt)
{
    /// <summary>Requires the original clock observation and both independent deadlines at each later publication boundary.</summary>
    internal bool Allows(TimeProvider clock, long watermark)
    {
        var now = ManagedLoginRules.Observe(clock, Math.Max(ObservedAt, watermark));
        return now < AssertionExpiresAt && now < CredentialExpiresAt;
    }
}
