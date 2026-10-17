namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Retains only a validated assertion's domain-bound digest and bounded UTC-second replay lifetime.</summary>
internal sealed class AdministratorClientAssertionUse
{
    public string Digest { get; set; } = string.Empty;
    public long CreatedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public long RetainUntil
    {
        get; set;
    }
}
