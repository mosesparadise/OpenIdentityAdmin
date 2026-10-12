namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator authentication state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorAuthentication(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorAuthentication
{
    public string UserId { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    /// <summary>Retains the highest committed factor destination, including expired or abandoned staging.</summary>
    public long LastAllocatedFactorGeneration
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public int Failures
    {
        get; set;
    }
    public long? LockoutUntil
    {
        get; set;
    }
    public long? ObservedAt
    {
        get; set;
    }
}
