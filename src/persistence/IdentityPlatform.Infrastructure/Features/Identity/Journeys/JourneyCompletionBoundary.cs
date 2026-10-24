using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Separates strict HTTP ceremony families while retaining the existing internal persisted-binding completion mechanism.</summary>
internal sealed class JourneyCompletionBoundary
{
    private readonly IssuerContinuationRequest? expected;
    internal static JourneyCompletionBoundary Standalone { get; } = new(null);

    /// <summary>Captures immutable intent/browser expectations; callers cannot substitute username, cookie session or timestamps.</summary>
    private JourneyCompletionBoundary(IssuerContinuationRequest? expected) => this.expected = expected;

    /// <summary>Requires an explicit validated browser request; null never means an unrestricted HTTP flow.</summary>
    internal static JourneyCompletionBoundary Browser(IssuerContinuationRequest request) => new(request ?? throw new ArgumentNullException(nameof(request)));

    /// <summary>Runs only after the transaction has validated all persisted inverse links and original deadlines.</summary>
    internal bool Matches(IssuerContinuation? row) => expected is null ? row is null :
        row is not null && row.IntentDigest == expected.IntentDigest && row.BrowserDigest == expected.BrowserDigest;
}
