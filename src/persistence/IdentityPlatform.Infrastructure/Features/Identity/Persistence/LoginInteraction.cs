namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class LoginInteraction
{
    public required string Id
    {
        get; set;
    }
    public required string RequestTokenId
    {
        get; set;
    }
    public required string RequestUri
    {
        get; set;
    }
    public required string ClientId
    {
        get; set;
    }
    public required string BrowserDigest
    {
        get; set;
    }
    public long CreatedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public string? CompletedSessionId
    {
        get; set;
    }
    public string? CompletedUserId
    {
        get; set;
    }
    public bool Consumed
    {
        get; set;
    }
}
