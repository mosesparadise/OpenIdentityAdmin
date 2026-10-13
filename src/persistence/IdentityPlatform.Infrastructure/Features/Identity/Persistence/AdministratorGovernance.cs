namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Serializes governance and records committed authority changes independently of lock acquisition.</summary>
/// <example><code>var singleton = new AdministratorGovernance(); // Seeded once by the native migration.</code></example>
public sealed class AdministratorGovernance
{
    public int Id { get; set; } = 1;
    public long Revision
    {
        get; set;
    }
    public bool BootstrapConsumed
    {
        get; set;
    }
    /// <summary>Advances once per accepted authority command; reads and authentication activity leave it unchanged.</summary>
    public long AuthorityRevision
    {
        get; set;
    }
}
