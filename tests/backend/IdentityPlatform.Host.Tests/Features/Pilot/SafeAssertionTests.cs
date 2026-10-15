using Xunit.Sdk;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed class SafeAssertionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_disclosure_regression_fails_without_reproducing_sensitive_content(int caseId)
    {
        string[] values = ["access_token", "id_token", "synthetic-password-sentinel", "synthetic-client-secret-sentinel", "synthetic-code-sentinel"];
        var forbidden = values[caseId];
        const string cookie = "synthetic-cookie-sentinel";
        var content = $"{{\"cookies\":[\"{cookie}\"],\"value\":\"{forbidden}\"}}";
        var failure = Record.Exception(() => SafeAssertions.ContainsNoSensitiveMaterial(content, forbidden));
        Assert.True(failure is FalseException, "The disclosure control did not produce the intended assertion failure.");
        var safe = !failure!.ToString().Contains(forbidden, StringComparison.Ordinal) &&
            !failure!.ToString().Contains(cookie, StringComparison.Ordinal) &&
            !failure!.ToString().Contains(content, StringComparison.Ordinal);
        Assert.True(safe, "The assertion diagnostic disclosed captured material.");
        Assert.Contains("Sensitive material was found", failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Safe_content_passes_the_same_assertion() =>
        SafeAssertions.ContainsNoSensitiveMaterial("Sign-in is temporarily unavailable.", "synthetic-code-sentinel");
}
