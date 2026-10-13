namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Bounds the complete internal governance decision; overflow always denies rather than truncating authority.</summary>
internal static class GovernanceLimits
{
    internal const int Accounts = 256;
    internal const int Memberships = 64;
    internal const int Roles = 128;
    internal const int Claims = 128;
    internal const int Singleton = 1;
}
