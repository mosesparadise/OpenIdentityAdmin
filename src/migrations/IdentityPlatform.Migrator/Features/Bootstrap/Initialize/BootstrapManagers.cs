using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

// Scoped to the one-shot operation; native managers otherwise use CancellationToken.None.
public sealed class BootstrapCancellation
{
    public CancellationToken Token
    {
        get; set;
    }
}

public sealed class BootstrapUserManager(IUserStore<PlatformUser> store, IOptions<IdentityOptions> options,
    IPasswordHasher<PlatformUser> hasher, IEnumerable<IUserValidator<PlatformUser>> userValidators,
    IEnumerable<IPasswordValidator<PlatformUser>> passwordValidators, ILookupNormalizer normalizer,
    IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<PlatformUser>> logger,
    BootstrapCancellation cancellation)
    : UserManager<PlatformUser>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    protected override CancellationToken CancellationToken => cancellation.Token;
}

public sealed class BootstrapRoleManager(IRoleStore<IdentityRole> store, IEnumerable<IRoleValidator<IdentityRole>> validators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, ILogger<RoleManager<IdentityRole>> logger,
    BootstrapCancellation cancellation)
    : RoleManager<IdentityRole>(store, validators, normalizer, errors, logger)
{
    protected override CancellationToken CancellationToken => cancellation.Token;
}
