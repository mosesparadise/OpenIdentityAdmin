namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class AdministratorEnrollment
{
    public required string UserId
    {
        get; set;
    }
    public string State { get; set; } = "Pending";
    public long Revision
    {
        get; set;
    }
}
