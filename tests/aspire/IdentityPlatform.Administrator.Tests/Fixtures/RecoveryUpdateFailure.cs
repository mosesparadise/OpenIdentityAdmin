using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Injects a native user-validation failure only after the real conditional recovery claim is observed in its transaction.</summary>
internal sealed class RecoveryUpdateFailure : IUserValidator<PlatformUser>
{
    internal bool Armed
    {
        get; set;
    }
    internal bool ObservedClaim
    {
        get; private set;
    }
    internal PlatformDbContext? Database
    {
        get; set;
    }

    /// <summary>Leaves real enrollment/password behavior unchanged and rejects the native update after actual redemption.</summary>
    public async Task<IdentityResult> ValidateAsync(UserManager<PlatformUser> manager, PlatformUser user)
    {
        if (!Armed)
        {
            return IdentityResult.Success;
        }
        ObservedClaim = await Database!.Set<AdministratorRecoveryCode>().AsNoTracking()
            .AnyAsync(row => row.UserId == user.Id && row.RedeemedAt != null, TestContext.Current.CancellationToken);
        const string Code = "SyntheticNativeUpdateRejected";
        return IdentityResult.Failed(new IdentityError { Code = Code });
    }
}
