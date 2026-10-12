namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator admission state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorAdmission(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorAdmission
{
    public int Id { get; set; } = 1;
    public long Revision
    {
        get; set;
    }
}
