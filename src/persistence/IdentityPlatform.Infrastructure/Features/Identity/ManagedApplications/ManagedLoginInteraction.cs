namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Retains native request and fresh managed-ceremony bindings independently of administrator sessions and destructive cleanup cascades.</summary>
internal sealed class ManagedLoginInteraction
{
    public string SelectorDigest { get; set; } = string.Empty;
    public string BrowserDigest { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string DemandDigest { get; set; } = string.Empty;
    public string? RequestId
    {
        get; set;
    }
    public long ApplicationRevision
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public ManagedLoginState State
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
    public long ObservedAt
    {
        get; set;
    }
    public long RetainUntil
    {
        get; set;
    }
    public long? RequestExpiresAt
    {
        get; set;
    }
    public long? NativeDependencyExpiresAt
    {
        get; set;
    }
    public long? NativeDependencyCreatedAt
    {
        get; set;
    }
    public Guid CorrelationId
    {
        get; set;
    }
    public Guid? CeremonyId
    {
        get; set;
    }
    public string? CeremonyBrowserDigest
    {
        get; set;
    }
    public string? BrowserBudgetId
    {
        get; set;
    }
    public string? UserId
    {
        get; set;
    }
    public string? SecurityStamp
    {
        get; set;
    }
    public long? AuthenticationGeneration
    {
        get; set;
    }
    public long? FactorGeneration
    {
        get; set;
    }
    public long? EnrollmentRevision
    {
        get; set;
    }
    public long? PasswordVerifiedAt
    {
        get; set;
    }
    public long? MfaVerifiedAt
    {
        get; set;
    }
    public long? ConsentVerifiedAt
    {
        get; set;
    }
    public string? AuthorizationId
    {
        get; set;
    }
    public string? CodeId
    {
        get; set;
    }
}

/// <summary>Distinguishes managed human proof phases from terminal denial, expiration and successful native code issuance.</summary>
internal enum ManagedLoginState
{
    PendingPassword = 1,
    PendingMfa = 2,
    PendingConsent = 3,
    Approved = 4,
    Consumed = 5,
    Denied = 6,
    Expired = 7
}
