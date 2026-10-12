using IdentityPlatform.Infrastructure.Features.Identity.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Separates native Identity dispatch from unavailable or protected factor material.</summary>
internal abstract class FactorStore
{
    /// <summary>Reads only the currently authorized factor selection.</summary>
    internal abstract Task<string?> ReadAsync(PlatformUser user, CancellationToken cancellationToken);
    /// <summary>Installs material only within an internally verified transaction capability.</summary>
    internal abstract Task WriteAsync(PlatformUser user, string key, CancellationToken cancellationToken);
    /// <summary>Replaces recovery verifiers in the caller's transaction.</summary>
    internal abstract Task ReplaceAsync(PlatformUser user, IEnumerable<string> codes, CancellationToken cancellationToken);
    /// <summary>Atomically claims one unredeemed verifier.</summary>
    internal abstract Task<bool> RedeemAsync(PlatformUser user, string code, CancellationToken cancellationToken);
    /// <summary>Counts unredeemed verifiers for the selected recovery generation.</summary>
    internal abstract Task<int> CountAsync(PlatformUser user, CancellationToken cancellationToken);
}

/// <summary>Preserves password-only hosts without resolving or creating a Data Protection ring.</summary>
internal sealed class UnavailableFactorStore : FactorStore
{
    /// <summary>Rejects native factor access in hosts without protected factor registration.</summary>
    internal override Task<string?> ReadAsync(PlatformUser user, CancellationToken cancellationToken) => throw new FactorUnavailableException();
    /// <summary>Rejects native factor access in hosts without protected factor registration.</summary>
    internal override Task WriteAsync(PlatformUser user, string key, CancellationToken cancellationToken) => throw new FactorUnavailableException();
    /// <summary>Rejects native factor access in hosts without protected factor registration.</summary>
    internal override Task ReplaceAsync(PlatformUser user, IEnumerable<string> codes, CancellationToken cancellationToken) => throw new FactorUnavailableException();
    /// <summary>Rejects native factor access in hosts without protected factor registration.</summary>
    internal override Task<bool> RedeemAsync(PlatformUser user, string code, CancellationToken cancellationToken) => throw new FactorUnavailableException();
    /// <summary>Rejects native factor access in hosts without protected factor registration.</summary>
    internal override Task<int> CountAsync(PlatformUser user, CancellationToken cancellationToken) => throw new FactorUnavailableException();
}
