using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Retains the original login deadline and one reserved native challenge for a browser generation.</summary>
internal sealed class BrowserLoginIntent
{
    public Guid CorrelationId
    {
        get; set;
    }
    public string Digest { get; set; } = string.Empty;
    public string BrowserDigest { get; set; } = string.Empty;
    public long Generation
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public long CreatedAt
    {
        get; set;
    }
    public long ObservedAt
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
    public BrowserIntentState State
    {
        get; set;
    }
    public string ClientId { get; set; } = AdministratorProtocolIdentity.ClientId;
    public string? NativeStateDigest
    {
        get; set;
    }
    public string? TicketDigest
    {
        get; set;
    }
}
