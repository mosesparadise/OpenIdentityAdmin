using System.ComponentModel.DataAnnotations;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Returns permanent application retirement separately from the evolving durable grant-revocation status.</summary>
/// <example><code>// Retain ApplicationVersion for an explicit stopped-job continuation; HTTP ETag identifies Job.Version.
/// var version = accepted.ApplicationVersion;</code></example>
public sealed record ApplicationRetirementAcceptance(
    [property: MaxLength(ApplicationProjection.IdentifierLength)] string ApplicationId,
    [property: MaxLength(GrantRevocationRules.MaximumVersionLength)] string ApplicationVersion,
    DateTimeOffset RetiredAt, [property: MaxLength(36)] string RequestKey, GrantJobView Job);

/// <summary>Provides a bounded durable recovery reference without disclosing approval actors or destructive request material.</summary>
/// <example><code>// Fetch this exact grant job after reading current retired application details.
/// var jobId = details.Retirement!.JobId;</code></example>
public sealed record ApplicationRetirementReference([property: MaxLength(36)] string JobId, DateTimeOffset RetiredAt);

/// <summary>Owns the fixed retirement routes and required terminal-state permission independently of ordinary edit authority.</summary>
/// <example><code>// Retirement also requires grants.revoke and fresh MFA; this permission alone never authorizes admission.
/// var permission = ApplicationRetirementRules.Permission;</code></example>
public static class ApplicationRetirementRules
{
    public const string Suffix = "/retirement";
    public const string ListPath = "/retired-applications";
    public const string Permission = "applications.disable";
}
