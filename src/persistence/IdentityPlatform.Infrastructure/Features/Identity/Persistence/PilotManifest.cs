namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class PilotManifest
{
    public string Id { get; set; } = "first-party-v1";
    public required string UserId
    {
        get; set;
    }
    public required string ApplicationId
    {
        get; set;
    }
    public required string ScopeId
    {
        get; set;
    }
    public required string UserName
    {
        get; set;
    }
    public required string RedirectUri
    {
        get; set;
    }
}
