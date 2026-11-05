using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.ApplicationGrants;

/// <summary>Returns sanitized committed progress; decimal counts remain lossless in browser clients and expiry bounds only this response's access.</summary>
/// <example><code>var completed = job.State == "succeeded"; // Keep AuthorizationCount and TokenCount as decimal strings.</code></example>
public sealed record GrantJobView(
    string Id,
    string ApplicationId,
    string Version,
    string State,
    string Failure,
    [property: RegularExpression("^(0|[1-9][0-9]{0,18})$")] string AuthorizationCount,
    [property: RegularExpression("^(0|[1-9][0-9]{0,18})$")] string TokenCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Contains at most the most recent ten operations for one exact application, with current read-access expiry.</summary>
/// <example><code>foreach (var job in page.Items) ShowProgress(job);</code></example>
public sealed record GrantJobPage([property: MaxLength(GrantRevocationRules.RecentLimit)] IReadOnlyList<GrantJobView> Items, DateTimeOffset ExpiresAt);

/// <summary>Binds durable acceptance or replay to the exact client request key without treating that key as authorization.</summary>
/// <example><code>if (accepted.RequestKey != submittedKey) RejectUnexpectedResponse();</code></example>
public sealed record GrantAcceptance(GrantJobView Job, string RequestKey);
