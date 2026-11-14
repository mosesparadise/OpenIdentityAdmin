using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Freezes only explicitly consented current Identity email fields; neither credentials nor arbitrary claims are projected.</summary>
internal sealed record ManagedEmailProjection(string Address, bool Verified)
{
    private const int MaximumLength = 256;

    /// <summary>Omits both fields when disclosure was not granted or email is absent; corrupt unbounded account data fails closed.</summary>
    internal static ManagedEmailProjection? Read(ManagedScopeDecision decision, PlatformUser user)
    {
        if (!decision.DisclosesEmail || string.IsNullOrEmpty(user.Email))
        {
            return null;
        }
        if (user.Email.Length > MaximumLength || user.Email.Any(char.IsControl))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return new(user.Email, user.EmailConfirmed);
    }
}
