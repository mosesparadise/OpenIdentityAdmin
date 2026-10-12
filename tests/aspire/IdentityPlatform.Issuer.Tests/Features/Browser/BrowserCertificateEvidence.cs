using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Requires certificate-specific evidence while tolerating the ordering of WebKit's two native failure channels.</summary>
internal static class BrowserCertificateEvidence
{
    private const string Chromium = "chromium";
    private const string Firefox = "firefox";
    private const string Webkit = "webkit";
    private const string ChromiumCertificate = "ERR_CERT_AUTHORITY_INVALID";
    private const string FirefoxIssuer = "SEC_ERROR_UNKNOWN_ISSUER";
    private const string FirefoxSelfSigned = "MOZILLA_PKIX_ERROR_SELF_SIGNED_CERT";
    private const string WebkitCertificate = "Unacceptable TLS certificate";
    private const int EvidenceSeconds = 2;
    private static readonly string[] NonCertificateFailures = ["cancelled", "Timeout", "Connection refused", "ERR_CERT_AUTHORITY_INVALID", ""];

    /// <summary>Matches only the current engine's native certificate diagnostic; transport cancellation never qualifies.</summary>
    private static bool IsCertificate(string engine, string? message) => message is not null && (engine switch
    {
        Chromium => message.Contains(ChromiumCertificate, StringComparison.Ordinal),
        Firefox => message.Contains(FirefoxIssuer, StringComparison.Ordinal) || message.Contains(FirefoxSelfSigned, StringComparison.Ordinal),
        Webkit => message.Contains(WebkitCertificate, StringComparison.Ordinal),
        _ => false
    });

    /// <summary>Excludes subresources, subframes and unrelated URLs from request-failure evidence.</summary>
    internal static bool Matches(string engine, string target, string actual, bool navigation, bool mainFrame, string? failure) =>
        navigation && mainFrame && string.Equals(target, actual, StringComparison.Ordinal) && IsCertificate(engine, failure);

    /// <summary>Requires one failed navigation on a fresh page and an engine-specific certificate error; never retries navigation.</summary>
    internal static async Task RequireAsync(IBrowserContext context, string target)
    {
        var page = await context.NewPageAsync();
        var certificate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Retain only successful classification, never arbitrary browser errors or request payloads.
        void Failed(object? sender, IRequest request)
        {
            if (request.IsNavigationRequest && string.Equals(target, request.Url, StringComparison.Ordinal) &&
                Matches(BrowserSession.Engine, target, request.Url, request.IsNavigationRequest,
                ReferenceEquals(request.Frame, page.MainFrame), request.Failure))
            {
                certificate.TrySetResult();
            }
        }
        page.RequestFailed += Failed;
        try
        {
            var error = await Assert.ThrowsAsync<PlaywrightException>(() => page.GotoAsync(target));
            if (!IsCertificate(BrowserSession.Engine, error.Message))
            {
                await certificate.Task.WaitAsync(TimeSpan.FromSeconds(EvidenceSeconds), TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            page.RequestFailed -= Failed;
            await page.CloseAsync();
        }
    }

    /// <summary>Proves cancellation, another request, a subframe and a wrong-engine marker cannot satisfy the negative control.</summary>
    internal static void AssertControls()
    {
        const string WebkitEngine = "webkit";
        const string ChromiumEngine = "chromium";
        const string FirefoxEngine = "firefox";
        const string WebkitError = "Unacceptable TLS certificate";
        const string ChromiumError = "ERR_CERT_AUTHORITY_INVALID";
        const string FirefoxError = "SEC_ERROR_UNKNOWN_ISSUER";
        const string OtherQuery = "?other";
        const string Target = "https://127.0.0.1:12345/alive";
        Assert.True(Matches(WebkitEngine, Target, Target, true, true, WebkitError));
        Assert.True(Matches(ChromiumEngine, Target, Target, true, true, ChromiumError));
        Assert.True(Matches(FirefoxEngine, Target, Target, true, true, FirefoxError));
        foreach (var failure in NonCertificateFailures)
        {
            Assert.False(Matches(WebkitEngine, Target, Target, true, true, failure));
        }
        Assert.False(Matches(WebkitEngine, Target, Target + OtherQuery, true, true, WebkitError));
        Assert.False(Matches(WebkitEngine, Target, Target, false, true, WebkitError));
        Assert.False(Matches(WebkitEngine, Target, Target, true, false, WebkitError));
        Assert.False(Matches(WebkitEngine, Target, Target, true, true, null));
    }
}
