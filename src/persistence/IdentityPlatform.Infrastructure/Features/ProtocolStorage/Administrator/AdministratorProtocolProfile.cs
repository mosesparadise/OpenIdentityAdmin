namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Binds the explicitly initialized native registration and serializes fixed-profile protocol mutations.</summary>
internal sealed class AdministratorProtocolProfile
{
    internal const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public string ApplicationId { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PublicKeyFingerprint { get; set; } = string.Empty;
    public bool Enabled
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public long LockRevision
    {
        get; set;
    }
    public long ObservedAt
    {
        get; set;
    }
}
