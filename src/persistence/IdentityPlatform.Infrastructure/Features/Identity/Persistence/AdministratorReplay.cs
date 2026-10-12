namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator replay state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorReplay(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorReplay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    public string ProtectedCode { get; set; } = string.Empty;
    public long AcceptedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
}
