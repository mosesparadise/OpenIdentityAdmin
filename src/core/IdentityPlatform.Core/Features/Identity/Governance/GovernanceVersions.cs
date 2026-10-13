namespace IdentityPlatform.Core.Features.Identity.Governance;

/// <summary>Binds a command to the observed authority revision and its explicit native targets.</summary>
/// <remarks>Witness versions are server-owned. Omit versions for targets that the action does not mutate.</remarks>
/// <example><code>var expected = new GovernanceVersions(revision, userStamp, roleStamp, enrollmentRevision);</code></example>
public sealed record GovernanceVersions(long AuthorityRevision, string? UserStamp = null,
    string? RoleStamp = null, long? EnrollmentRevision = null);
