using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Retains native Identity orchestration while enforcing high-entropy recovery generation and explicit installation.</summary>
internal sealed class AdministratorUserManager(IUserStore<PlatformUser> store, IOptions<IdentityOptions> options,
    IPasswordHasher<PlatformUser> hasher, IEnumerable<IUserValidator<PlatformUser>> userValidators,
    IEnumerable<IPasswordValidator<PlatformUser>> passwordValidators, ILookupNormalizer normalizer,
    IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<PlatformUser>> logger, FactorAccess access)
    : UserManager<PlatformUser>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    protected override CancellationToken CancellationToken => access.Token;

    /// <summary>Uses 128 random bits per recovery code rather than the framework's lower-entropy default format.</summary>
    protected override string CreateTwoFactorRecoveryCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(RecoveryVerifier.RandomBytes));

    /// <summary>Rejects unsupported counts before the native manager generates or replaces any material.</summary>
    public override Task<IEnumerable<string>?> GenerateNewTwoFactorRecoveryCodesAsync(PlatformUser user, int number)
    {
        if (number != RecoveryVerifier.Count)
        {
            throw new FactorUnavailableException();
        }
        return base.GenerateNewTwoFactorRecoveryCodesAsync(user, number);
    }

    /// <summary>Prevents ordinary reset from generating a different unproven active seed.</summary>
    public override Task<IdentityResult> ResetAuthenticatorKeyAsync(PlatformUser user) => throw new FactorUnavailableException();

    /// <summary>Installs only the verified staged seed through the protected native store; never commits or issues authority.</summary>
    internal async Task InstallAsync(VerifiedStagedFactor proof, CancellationToken cancellationToken)
    {
        using var installation = access.Install(proof, cancellationToken);
        var user = await FindByIdAsync(proof.UserId) ?? throw new FactorUnavailableException();
        await ((IUserAuthenticatorKeyStore<PlatformUser>)Store).SetAuthenticatorKeyAsync(user, proof.Seed, cancellationToken);
        Require(await UpdateAsync(user));
        Require(await SetTwoFactorEnabledAsync(user, true));
        Require(await UpdateSecurityStampAsync(user));
    }

    /// <summary>Converts native manager failures into a fixed failure so the owning transaction cannot silently continue.</summary>
    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new FactorUnavailableException();
        }
    }
}
