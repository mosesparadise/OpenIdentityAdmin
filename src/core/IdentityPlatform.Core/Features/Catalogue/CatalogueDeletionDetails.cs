using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Confirms one versioned catalogue deletion with a caller-retained canonical idempotency identity.</summary>
/// <example><code>var request = new CatalogueDeleteInput(true, Guid.NewGuid().ToString("D"));</code></example>
public sealed record CatalogueDeleteInput(bool Confirmed, [property: Required] string RequestKey);

/// <summary>Reports durable state independently of the deleted resource without exposing approval, account or audit internals.</summary>
/// <example><code>var status = new CatalogueDeletionDetails(jobId, "scope", scopeId, "queued", "none", createdAt, observedAt, expiresAt);</code></example>
public sealed record CatalogueDeletionDetails(Guid Id, string Kind, string TargetId, string State, string Failure,
    DateTimeOffset CreatedAt, DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt);
