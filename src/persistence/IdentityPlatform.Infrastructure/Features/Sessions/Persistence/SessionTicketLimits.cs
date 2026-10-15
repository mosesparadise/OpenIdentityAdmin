namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Owns the storage and work bounds shared by orchestration and provider mappings.</summary>
internal static class SessionTicketLimits
{
    internal const int DigestLength = 64;
    internal const int BindingLength = 400;
    internal const int NativeBytes = 65_536;
    internal const int ProtectedBytes = 98_304;
    internal const int CleanupBatch = 100;
    internal static readonly TimeSpan MaximumIdle = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan MaximumAbsolute = TimeSpan.FromHours(8);
}
