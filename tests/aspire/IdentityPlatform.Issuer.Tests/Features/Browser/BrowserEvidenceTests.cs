using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class BrowserEvidenceTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Sensitive_browser_and_timeout_diagnostics_are_suppressed_but_assertions_remain_failures(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        var sentinel = "synthetic-private-action-canary-" + Guid.NewGuid().ToString("N");
        foreach (var original in new Exception[] { new PlaywrightException(sentinel), new TimeoutException(sentinel) })
        {
            var safe = await Record.ExceptionAsync(() => BrowserEvidence.RunAsync(() => Task.FromException(original)));
            Assert.True(safe is Xunit.Sdk.XunitException, "Browser failure sanitization did not preserve a safe test failure.");
            Assert.True(!safe!.ToString().Contains(sentinel, StringComparison.Ordinal), "Sensitive browser diagnostic reached test evidence.");
            Assert.True(safe.InnerException is null, "Original browser diagnostics must not survive as an inner exception.");
        }
        var phased = await Record.ExceptionAsync(() => BrowserEvidence.RunAsync(async () =>
        {
            await Task.Yield();
            BrowserEvidence.Mark(BrowserPhase.TrustedIssuer);
            throw new PlaywrightException(sentinel);
        }));
        Assert.True(phased is Xunit.Sdk.XunitException && phased.Message.Contains("TrustedIssuer", StringComparison.Ordinal) &&
            !phased.ToString().Contains(sentinel, StringComparison.Ordinal), "Async browser phase diagnostics must remain accurate and secret-safe.");
        var assertion = new Xunit.Sdk.XunitException("Expected invariant failure");
        var preserved = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => BrowserEvidence.RunAsync(() => Task.FromException(assertion)));
        Assert.Same(assertion, preserved);
        Assert.Throws<InvalidOperationException>(() => BrowserSession.SelectEngine("unsupported"));
        Assert.Equal("chromium", BrowserSession.SelectEngine(null));
        Assert.Equal("firefox", BrowserSession.SelectEngine("firefox"));
        Assert.Equal("webkit", BrowserSession.SelectEngine("webkit"));
    }
}
