namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator recoverycode state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorRecoveryCode(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorRecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    public string Verifier { get; set; } = string.Empty;
    public long? RedeemedAt
    {
        get; set;
    }
}
