using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks purpose isolation, original time bounds and canonical browser selectors using real native protection.</summary>
public sealed class BrowserLoginProofTests
{
    private const int IntentMinutes = 5;
    private const int BeforeExpiryMinutes = 4;
    private const int Generation = 3;
    private const int Truncation = 4;
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A protected generation cannot serve as prelogin state, and stale or excessively future proof never resets to generation zero.</summary>
    [Theory]
    [InlineData(-31)]
    [InlineData(300)]
    [InlineData(301)]
    public void Given_ProtectedBrowserGeneration_When_OriginalLifetimeIsInvalid_Then_ProofDenies(int seconds)
    {
        var proof = new BrowserLoginProof(new EphemeralDataProtectionProvider());
        var generation = new BrowserGenerationProof(BrowserLoginSelector.CreateSelector(), 0, Start.UtcTicks, Start.AddMinutes(IntentMinutes).UtcTicks);
        var encoded = proof.Protect(generation);
        Assert.NotNull(proof.Generation(encoded, Start));
        Assert.Null(proof.Pending(encoded, Start));
        Assert.Null(proof.Generation(encoded, Start.AddSeconds(seconds)));
    }

    /// <summary>Pending proof retains the exact intent/browser/generation/deadline; mutation, wrong purpose and expiry fail closed.</summary>
    [Fact]
    public void Given_ProtectedPendingLogin_When_ProofIsReadOrSubstituted_Then_OnlyOriginalEnvelopeIsAccepted()
    {
        var proof = new BrowserLoginProof(new EphemeralDataProtectionProvider());
        var intent = BrowserLoginSelector.CreateSelector();
        var browser = BrowserLoginSelector.Digest(BrowserLoginSelector.CreateSelector())!;
        var pending = new BrowserPendingProof(intent, browser, Generation, Start.UtcTicks, Start.AddMinutes(IntentMinutes).UtcTicks);
        var encoded = proof.Protect(pending);
        var accepted = Assert.IsType<BrowserPendingProof>(proof.Pending(encoded, Start.AddMinutes(BeforeExpiryMinutes)));
        Assert.True(accepted.Intent == intent && accepted.BrowserDigest == browser && accepted.Generation == Generation);
        Assert.Equal(pending.Expires, accepted.Expires);
        Assert.Null(proof.Generation(encoded, Start));
        Assert.Null(proof.Pending(encoded[..^Truncation], Start));
        Assert.Null(proof.Pending(encoded, Start.AddMinutes(IntentMinutes)));
    }
}
