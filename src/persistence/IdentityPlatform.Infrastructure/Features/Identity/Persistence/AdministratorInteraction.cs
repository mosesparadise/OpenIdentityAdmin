namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Persists bounded administrator interaction state; this row alone grants no authentication authority.</summary>
/// <example><code>var row = new AdministratorInteraction(); // Populate and validate inside the owning transaction.</code></example>
public sealed class AdministratorInteraction
{
    public Guid Id
    {
        get; set;
    } = Guid.NewGuid();
    public string PublicId
    {
        get; set;
    } = string.Empty;
    public string UserId
    {
        get; set;
    } = string.Empty;
    public string BrowserBudgetId
    {
        get; set;
    } = string.Empty;
    public string BrowserDigest
    {
        get; set;
    } = string.Empty;
    public string Purpose
    {
        get; set;
    } = string.Empty;
    private const string PendingState = "Pending";
    public string State
    {
        get; set;
    } = PendingState;
    public string SecurityStamp
    {
        get; set;
    } = string.Empty;
    public long Generation
    {
        get; set;
    } = 1;
    public long DestinationGeneration
    {
        get; set;
    } = 1;
    /// <summary>Binds new journeys to the enrolled state revision; older null rows cannot authorize activation.</summary>
    public long? ExpectedEnrollmentRevision

    {
        get; set;
    }
    /// <summary>Identifies the actual proof that authorized staging; null means no staging authority.</summary>
    public string? AuthorizationKind
    {
        get; set;
    }
    /// <summary>Records the actual step-up verification time without refreshing password freshness.</summary>
    public long? AuthorizationVerifiedAt
    {
        get; set;
    }
    /// <summary>Binds authorized staging to the factor current when step-up occurred.</summary>
    public long? SourceFactorGeneration
    {
        get; set;
    }
    /// <summary>Requires the initiating full-session cookie for voluntary security actions.</summary>
    public string? SessionPublicId
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
    public long PasswordVerifiedAt

    {
        get; set;
    }
    public int Attempts

    {
        get; set;
    }
    public long Revision

    {
        get; set;
    }
    /// <summary>Requires the persisted browser continuation when this real login interaction completes.</summary>
    public string? BrowserIntentDigest
    {
        get; set;
    }
    public string? StagedSeed

    {
        get; set;
    }
}
