namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class AdministratorBootstrapReceipt
{
    public int Id { get; set; } = 1;
    public Guid OperationId
    {
        get; set;
    }
    public required string OriginalNormalizedUserName
    {
        get; set;
    }
    public required string UserId
    {
        get; set;
    }
    public int BundleVersion
    {
        get; set;
    }
    public long CompletedAt
    {
        get; set;
    }
}
