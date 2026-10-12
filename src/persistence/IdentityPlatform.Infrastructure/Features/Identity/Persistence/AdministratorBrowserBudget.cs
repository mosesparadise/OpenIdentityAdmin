namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator browserbudget state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorBrowserBudget(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorBrowserBudget
{
    public string Id { get; set; } = string.Empty;
    public long Revision
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
}
