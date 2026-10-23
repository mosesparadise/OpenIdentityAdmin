namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Captures separate original intent and issuer-browser proof digests; it carries no credential or authenticated account.</summary>
internal sealed class IssuerContinuationRequest
{
    internal string IntentDigest
    {
        get;
    }
    internal string BrowserDigest
    {
        get;
    }

    /// <summary>Constructs only after both canonical selector representations have been validated.</summary>
    private IssuerContinuationRequest(string intent, string browser)
    {
        IntentDigest = intent;
        BrowserDigest = browser;
    }

    /// <summary>Rejects malformed or reused proof values before anonymous admission or password binding.</summary>
    internal static IssuerContinuationRequest? Read(string intent, string browser) =>
        IssuerContinuationPolicy.Digest(intent) is { } intentDigest && IssuerContinuationPolicy.Digest(browser) is { } browserDigest &&
        intentDigest != browserDigest ? new(intentDigest, browserDigest) : null;
}
