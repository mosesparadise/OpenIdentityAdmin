namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Tracks enrollment without treating a pending row or factor generation as authentication proof.</summary>
/// <example><code>var enrollment = new AdministratorEnrollment { UserId = user.Id }; // Remains Pending.</code></example>
public sealed class AdministratorEnrollment
{
    public required string UserId
    {
        get; set;
    }
    public string State { get; set; } = "Pending";
    public bool Suspended
    {
        get; set;
    }
    public long FactorGeneration
    {
        get; set;
    }
    public long RecoveryGeneration
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
}
