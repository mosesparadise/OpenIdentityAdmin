namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

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
}
