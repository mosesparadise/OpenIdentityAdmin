using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Constrains the narrow reconciliation antiforgery purpose without weakening normal generation admission.</summary>
public sealed class BrowserLogoutBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    private const int LifetimeMinutes = 5;
    private const int SuccessorGeneration = 2;
    private const string Browser = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";

    /// <summary>An original proof can bind only its immediate logout successor, with a distinct purpose and its old generation.</summary>
    [Fact]
    public void Given_ImmediateLogoutTombstone_When_AntiforgeryBindingIsBuilt_Then_OrdinaryMutationBindingRemainsDistinct()
    {
        var proof = new BrowserGenerationProof(Browser, 1, Now.UtcTicks, Now.AddMinutes(LifetimeMinutes).UtcTicks);
        var result = new BrowserLogoutResult(SuccessorGeneration, false, Now.UtcTicks, Now.AddMinutes(LifetimeMinutes).UtcTicks);
        var row = new BrowserContext
        {
            Digest = proof.Digest,
            Generation = 1,
            CorrelationId = Guid.NewGuid(),
            Revision = 1,
            ObservedAt = Now.UtcTicks,
            ExpiresAt = Now.AddMinutes(LifetimeMinutes).UtcTicks
        };
        var ordinary = BrowserRequestBinding.FromRead(proof.Digest, 1, row, Now);
        var recovery = BrowserRequestBinding.FromLogoutReconciliation(proof, result, Now);
        Assert.NotNull(ordinary);
        Assert.NotNull(recovery);
        Assert.NotEqual(ordinary.AdditionalData, recovery.AdditionalData);
        Assert.Equal("browser-logout-reconciliation-v1:" + proof.Digest + ":1", recovery.AdditionalData);
        row.Generation = SuccessorGeneration;
        Assert.Null(BrowserRequestBinding.FromRead(proof.Digest, 1, row, Now));
    }

    /// <summary>Zero, overflow, wrong successor, remote reservation and expired committed bounds cannot produce a recovery token binding.</summary>
    [Theory]
    [InlineData(0, 1, false, false)]
    [InlineData(long.MaxValue, long.MaxValue, false, false)]
    [InlineData(1, 1, false, false)]
    [InlineData(1, 3, false, false)]
    [InlineData(1, 2, true, false)]
    [InlineData(1, 2, false, true)]
    public void Given_InvalidReconciliationBounds_When_BindingIsRequested_Then_NoPurposeIsIssued(long previous, long current, bool remote, bool expired)
    {
        var proof = new BrowserGenerationProof(Browser, previous, Now.UtcTicks, Now.AddMinutes(LifetimeMinutes).UtcTicks);
        var result = new BrowserLogoutResult(current, remote, Now.UtcTicks, Now.AddMinutes(LifetimeMinutes).UtcTicks);
        Assert.Null(BrowserRequestBinding.FromLogoutReconciliation(proof, result, expired ? Now.AddMinutes(LifetimeMinutes) : Now));
    }
}
