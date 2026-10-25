using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires original correlation bounds and separate native protection before any return can reach persistence.</summary>
public sealed class BrowserLogoutProofTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const int LifetimeSeconds = 300;
    private const int Generation = 2;

    /// <summary>Only the original state, browser and generation can use a protected logout envelope; no login purpose is interchangeable.</summary>
    [Fact]
    public void Given_ProtectedLogout_When_StateBrowserOrPurposeChanges_Then_ReturnBindingDenies()
    {
        var provider = new EphemeralDataProtectionProvider();
        var proofs = new BrowserLogoutProof(provider);
        var browser = new BrowserGenerationProof(BrowserLoginSelector.CreateSelector(), Generation, Start.UtcTicks, Start.AddSeconds(LifetimeSeconds).UtcTicks);
        var state = BrowserLoginSelector.CreateSelector();
        var pending = new BrowserPendingProof(state, browser.Digest, Generation, browser.Created, browser.Expires);
        var encoded = proofs.Protect(pending);
        Assert.NotNull(proofs.Read(encoded, state, browser, Start));
        Assert.Null(proofs.Read(encoded, BrowserLoginSelector.CreateSelector(), browser, Start));
        Assert.Null(proofs.Read(encoded, state, new(browser.Browser, Generation + 1, browser.Created, browser.Expires), Start));
        Assert.Null(proofs.Read(encoded, state, new(BrowserLoginSelector.CreateSelector(), Generation, browser.Created, browser.Expires), Start));
        Assert.Null(new BrowserLoginProof(provider).Pending(encoded, Start));
        Assert.Null(proofs.Read(new BrowserLoginProof(provider).Protect(pending), state, browser, Start));
        Assert.Null(new BrowserLogoutProof(new EphemeralDataProtectionProvider()).Read(encoded, state, browser, Start));
    }

    /// <summary>Return parsing keeps the original five-minute ceiling and rejects equality, excessive rollback and corrupted ciphertext.</summary>
    [Theory]
    [InlineData(-31)]
    [InlineData(300)]
    [InlineData(301)]
    public void Given_OriginalLogoutBounds_When_ReturnIsOutsideLifetime_Then_ProofDenies(int seconds)
    {
        var proofs = new BrowserLogoutProof(new EphemeralDataProtectionProvider());
        var browser = new BrowserGenerationProof(BrowserLoginSelector.CreateSelector(), Generation, Start.UtcTicks, Start.AddSeconds(LifetimeSeconds).UtcTicks);
        var state = BrowserLoginSelector.CreateSelector();
        var encoded = proofs.Protect(new(state, browser.Digest, Generation, browser.Created, browser.Expires));
        Assert.NotNull(proofs.Read(encoded, state, browser, Start.AddSeconds(LifetimeSeconds - 1)));
        Assert.Null(proofs.Read(encoded, state, browser, Start.AddSeconds(seconds)));
        Assert.Null(proofs.Read(encoded[..^1], state, browser, Start));
    }
}
