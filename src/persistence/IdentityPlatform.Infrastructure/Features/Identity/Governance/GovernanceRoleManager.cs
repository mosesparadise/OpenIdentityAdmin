using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Propagates the original transaction cancellation through native role validation and persistence.</summary>
internal sealed class GovernanceRoleManager(IRoleStore<IdentityRole> store, IEnumerable<IRoleValidator<IdentityRole>> validators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, ILogger<RoleManager<IdentityRole>> logger, NativeOperation operation)
    : RoleManager<IdentityRole>(store, validators, normalizer, errors, logger)
{
    protected override CancellationToken CancellationToken => operation.Token;

    /// <summary>Rejects a substituted scope or token before native role operations can leave the command lifetime.</summary>
    internal void RequireOperation(PlatformDbContext database, CancellationToken token) => operation.RequireContext(database, token);
}
