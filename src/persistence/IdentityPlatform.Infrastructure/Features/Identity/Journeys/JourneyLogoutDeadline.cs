using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Preserves the protected confirmation's original clock bounds through locked revocation and publication.</summary>
internal readonly record struct JourneyLogoutDeadline(long Created, long Expires)
{
    private const int MaximumSeconds = 300;

    /// <summary>Denies rollback, malformed windows and equality at the unchanged confirmation deadline.</summary>
    internal void RequireCurrent(long now)
    {
        if (Created <= 0 || Expires <= Created || Expires - Created > MaximumSeconds || now < Created || now >= Expires)
        {
            throw new FactorUnavailableException();
        }
    }
}
