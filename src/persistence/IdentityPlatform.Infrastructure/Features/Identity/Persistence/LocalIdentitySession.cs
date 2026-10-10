namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class LocalIdentitySession
{
    public required string Id
    {
        get; set;
    }
    public required string UserId
    {
        get; set;
    }
    public required string SecurityStamp
    {
        get; set;
    }
    public long AuthenticatedAt
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
}
