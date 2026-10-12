using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Keeps native password/role behavior while prohibiting Identity's plaintext factor token fallback.</summary>
internal sealed class PlatformIdentityStore(PlatformDbContext database, FactorStore factors, IdentityErrorDescriber errors)
    : UserStore<PlatformUser>(database, errors)
{

    /// <summary>Dispatches native authenticator reading through the protected factor adapter.</summary>
    public override Task<string?> GetAuthenticatorKeyAsync(PlatformUser user, CancellationToken cancellationToken = default) => factors.ReadAsync(user, cancellationToken);
    /// <summary>Dispatches native verified authenticator installation through the protected factor adapter.</summary>
    public override Task SetAuthenticatorKeyAsync(PlatformUser user, string key, CancellationToken cancellationToken = default) => factors.WriteAsync(user, key, cancellationToken);
    /// <summary>Dispatches native recovery replacement through the protected factor adapter.</summary>
    public override Task ReplaceCodesAsync(PlatformUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default) => factors.ReplaceAsync(user, recoveryCodes, cancellationToken);
    /// <summary>Dispatches native single-use recovery redemption through the protected factor adapter.</summary>
    public override Task<bool> RedeemCodeAsync(PlatformUser user, string code, CancellationToken cancellationToken = default) => factors.RedeemAsync(user, code, cancellationToken);
    /// <summary>Dispatches native recovery counting through the protected factor adapter.</summary>
    public override Task<int> CountCodesAsync(PlatformUser user, CancellationToken cancellationToken = default) => factors.CountAsync(user, cancellationToken);

    /// <summary>Rejects reserved token writes even when reached through UserManager.SetAuthenticationTokenAsync.</summary>
    public override Task SetTokenAsync(PlatformUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken = default)
    {
        RejectReserved(loginProvider);
        return base.SetTokenAsync(user, loginProvider, name, value, cancellationToken);
    }

    /// <summary>Rejects reserved token reads instead of treating legacy plaintext as usable factor material.</summary>
    public override Task<string?> GetTokenAsync(PlatformUser user, string loginProvider, string name, CancellationToken cancellationToken = default)
    {
        RejectReserved(loginProvider);
        return base.GetTokenAsync(user, loginProvider, name, cancellationToken);
    }

    /// <summary>Prevents generic token removal from bypassing the governed factor lifecycle.</summary>
    public override Task RemoveTokenAsync(PlatformUser user, string loginProvider, string name, CancellationToken cancellationToken = default)
    {
        RejectReserved(loginProvider);
        return base.RemoveTokenAsync(user, loginProvider, name, cancellationToken);
    }

    /// <summary>Restricts generic provider IDs to canonical ASCII, excluding framework brackets and provider-collation aliases.</summary>
    private static void RejectReserved(string provider)
    {
        if (string.IsNullOrEmpty(provider) || provider.Any(character =>
            character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')))
        {
            throw new FactorUnavailableException();
        }
    }
}
