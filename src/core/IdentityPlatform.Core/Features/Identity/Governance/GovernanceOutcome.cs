namespace IdentityPlatform.Core.Features.Identity.Governance;

/// <summary>Reports a fixed internal result without exposing credentials or native failure details.</summary>
/// <example><code>var unchanged = GovernanceOutcome.NoChange;</code></example>
public enum GovernanceOutcome
{
    Denied,
    NoChange,
    Accepted
}
