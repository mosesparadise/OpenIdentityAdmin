namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator session state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorSession(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AuthenticationMethods { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = string.Empty;
    public long Generation { get; set; } = 1;
    public long FactorGeneration { get; set; } = 1;
    public long PasswordVerifiedAt
    {
        get; set;
    }
    public long FactorVerifiedAt
    {
        get; set;
    }
    public long CreatedAt
    {
        get; set;
    }
    public long LastActivityAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public bool Revoked
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
}
