namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class LoginBrowserBudget
{
    public required string Id
    {
        get; set;
    }
    public long LastAdmissionAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
}
