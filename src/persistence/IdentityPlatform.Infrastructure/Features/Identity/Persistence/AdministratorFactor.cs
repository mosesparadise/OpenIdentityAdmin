namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator factor state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorFactor(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorFactor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    public string ProtectedSeed { get; set; } = string.Empty;
}
