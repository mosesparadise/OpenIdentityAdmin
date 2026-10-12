namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator interaction state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorInteraction(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string BrowserBudgetId { get; set; } = string.Empty;
    public string BrowserDigest { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    private const string PendingState = "Pending";
    public string State { get; set; } = PendingState;
    public string SecurityStamp { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    public long DestinationGeneration { get; set; } = 1;
    public long CreatedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public long PasswordVerifiedAt
    {
        get; set;
    }
    public int Attempts
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public string? StagedSeed
    {
        get; set;
    }
}
