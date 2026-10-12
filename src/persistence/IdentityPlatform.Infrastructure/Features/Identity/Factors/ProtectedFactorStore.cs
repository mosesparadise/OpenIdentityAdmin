using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Stores protected native authenticator material; ordinary native reset cannot install a key.</summary>
internal sealed class ProtectedFactorStore(PlatformDbContext database, FactorProtection protection,
    FactorAccess access, ProtectedRecoveryStore recovery) : FactorStore
{
    private const string Active = "Active";

    /// <summary>Reads a selected staged key, or the current active generation; never consults native token storage.</summary>
    internal override async Task<string?> ReadAsync(PlatformUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (access.Staged is { } staged)
        {
            access.RequireCurrent();
            if (!string.Equals(staged.UserId, user.Id, StringComparison.Ordinal) || staged.StagedSeed is null)
            {
                throw new FactorUnavailableException();
            }
            return protection.Unprotect(staged.StagedSeed, user.Id, staged.DestinationGeneration, FactorProtection.Staged, staged.PublicId);
        }
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleOrDefaultAsync(value => value.UserId == user.Id, cancellationToken);
        if (enrollment is null || !string.Equals(enrollment.UserId, user.Id, StringComparison.Ordinal) || enrollment.Suspended || enrollment.State != Active || enrollment.FactorGeneration <= 0)
        {
            throw new FactorUnavailableException();
        }
        var factor = await database.Set<AdministratorFactor>().AsNoTracking().SingleOrDefaultAsync(value =>
            value.UserId == user.Id && value.Generation == enrollment.FactorGeneration, cancellationToken);
        return factor is null || !string.Equals(factor.UserId, user.Id, StringComparison.Ordinal) ? throw new FactorUnavailableException() :
            protection.Unprotect(factor.ProtectedSeed, factor.UserId, factor.Generation, FactorProtection.Active);
    }

    /// <summary>Stages exactly the seed proven by native validation; the caller still owns commit, state, replay and audit.</summary>
    internal override Task WriteAsync(PlatformUser user, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        access.RequireCurrent();
        var proof = access.Installation ?? throw new FactorUnavailableException();
        proof.Consume(database, user, key);
        database.Add(new AdministratorFactor
        {
            UserId = user.Id,
            Generation = proof.Generation,
            ProtectedSeed = protection.Protect(key, user.Id, proof.Generation, FactorProtection.Active)
        });
        return Task.CompletedTask;
    }

    /// <summary>Dispatches native recovery replacement through the protected factor adapter.</summary>
    internal override Task ReplaceAsync(PlatformUser user, IEnumerable<string> codes, CancellationToken cancellationToken) => recovery.ReplaceAsync(user, codes, cancellationToken);
    /// <summary>Dispatches native single-use recovery redemption through the protected factor adapter.</summary>
    internal override Task<bool> RedeemAsync(PlatformUser user, string code, CancellationToken cancellationToken) => recovery.RedeemAsync(user, code, cancellationToken);
    /// <summary>Dispatches native recovery counting through the protected factor adapter.</summary>
    internal override Task<int> CountAsync(PlatformUser user, CancellationToken cancellationToken) => recovery.CountAsync(user, cancellationToken);
}
