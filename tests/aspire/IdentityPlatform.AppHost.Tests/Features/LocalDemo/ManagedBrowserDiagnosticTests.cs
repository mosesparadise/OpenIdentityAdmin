using IdentityPlatform.Administrator.Tests.Features.Browser;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Seeds private URL and exception material into the actual diagnostic producer and requires only finite output.</summary>
public sealed class ManagedBrowserDiagnosticTests
{
    private const string Secret = "synthetic-private-canary";
    private const string OtherLocation = "location=Other";
    private const string CallbackLocation = "location=RpCallback";
    private const string Status = "token-status=503";
    private const string UnknownStatus = "token-status=0";
    private const string FailedCompletion = "completion=Failure";
    private const string Unexpected = "failure=Unexpected";
    private const string Repeated = "repeated=True";
    private const string CallbackPath = "/callback?code=";
    private const int Unavailable = 503;
    private static readonly Uri Issuer = new("https://issuer.example.test/");
    private static readonly Uri RelyingParty = new("https://client.example.test/");

    /// <summary>Known and unknown destinations, arbitrary exception text and invalid numeric values cannot disclose private input.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_PrivateProtocolValues_When_DiagnosticIsProduced_Then_OnlyClosedProgressAndLocationRemain()
    {
        var diagnostic = new ManagedBrowserDiagnostic { Repeated = true, Current = ManagedBrowserDiagnostic.Stage.TokenShapeAccepted };
        diagnostic.Response(Unavailable);
        diagnostic.Failed(new InvalidOperationException(Secret, new InvalidDataException(Secret)));
        var value = diagnostic.Describe(new Uri(RelyingParty, CallbackPath + Secret).AbsoluteUri, Issuer, RelyingParty, 1, 1, false);
        Assert.DoesNotContain(Secret, value, StringComparison.Ordinal);
        Assert.Contains(CallbackLocation, value, StringComparison.Ordinal);
        Assert.Contains(Status, value, StringComparison.Ordinal);
        Assert.Contains(Unexpected, value, StringComparison.Ordinal);
        Assert.Contains(FailedCompletion, value, StringComparison.Ordinal);
        Assert.Contains(Repeated, value, StringComparison.Ordinal);
        diagnostic.Response(int.MaxValue);
        value = diagnostic.Describe(new Uri(Issuer, Secret).AbsoluteUri, Issuer, RelyingParty, int.MinValue, int.MaxValue, null);
        Assert.DoesNotContain(Secret, value, StringComparison.Ordinal);
        Assert.Contains(OtherLocation, value, StringComparison.Ordinal);
        Assert.Contains(UnknownStatus, value, StringComparison.Ordinal);
    }
}
