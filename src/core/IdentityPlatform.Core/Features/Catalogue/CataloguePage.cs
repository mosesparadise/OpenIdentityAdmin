namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Contains only bounded catalogue presentation metadata; detailed references and policy are read separately.</summary>
/// <example><code>var item = new CatalogueSummary(id, "orders.read", "Read orders", true);</code></example>
public sealed record CatalogueSummary(string Id, string Name, string DisplayName, bool Enabled);

/// <summary>Publishes one bounded catalogue page with its original authority deadline and a lookahead indicator.</summary>
/// <example><code>var page = new CataloguePage(items, 1, 25, false, expiry);</code></example>
public sealed record CataloguePage(IReadOnlyList<CatalogueSummary> Items, int Page, int PageSize, bool HasMore, DateTimeOffset ExpiresAt);
