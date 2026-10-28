using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Verifies that disabled-profile proof consumption preserves existing durable quota, retention and clock semantics.</summary>
internal static class CleanupAssertionBounds
{
    private const int Capacity = 10000;
    private const int CleanupBatch = 100;
    private const int ExpiredRows = 101;
    private const int RetentionSeconds = 30;
    private const int ProofLifetime = 60;
    private const int ShortLifetime = 10;
    private const int ForwardSeconds = 50;
    private const int WatermarkAdvance = 20;
    private const int OutsideRollback = 31;
    private const int OldAge = 100;
    private const string ExactRetention = "Cleanup must remove only the exact eligible batch and preserve every live digest.";

    /// <summary>Uses the real disabled branch for lifetime, speculative rollback, monotonic time and exact retained capacity.</summary>
    internal static async Task VerifyAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        await CleanupAssertionFixture.SetEnabledAsync(fixture.First, false, token);
        await ExpiryAsync(fixture, token);
        await SpeculativeCleanupAsync(fixture, token);
        await ClockRollbackAsync(fixture, token);
        await CapacityAsync(fixture, token);
    }

    /// <summary>Exact expiry denies an unconsumed assertion and leaves no row, even when native validation permits clock skew.</summary>
    private static async Task ExpiryAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = fixture.Proof();
        var clock = fixture.First.GetRequiredService<NativeConsumptionClock>();
        clock.Set(fixture.Now.AddSeconds(ProofLifetime));
        try
        {
            await CleanupAssertionEligibility.DeniedAsync(fixture.First, proof.Text, token);
        }
        finally { clock.Set(fixture.Now); }
        Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, proof.Text, token);
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
    }

    /// <summary>A duplicate long proof rolls back an actually reached delete, retaining the short proof across the controlled clock rollback.</summary>
    private static async Task SpeculativeCleanupAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var shortProof = fixture.Proof(ShortLifetime);
        var longProof = fixture.Proof();
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, shortProof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, longProof.Text, token);
        var clock = fixture.First.GetRequiredService<NativeConsumptionClock>();
        var fault = fixture.First.GetRequiredService<NativeAssertionCleanupClock>();
        var deleted = 0;
        fault.AfterDelete = () => { deleted++; clock.Set(fixture.Now); };
        clock.Set(fixture.Now.AddSeconds(ForwardSeconds));
        try
        {
            await CleanupAssertionEligibility.DeniedAsync(fixture.First, longProof.Text, token);
        }
        finally { fault.AfterDelete = null; clock.Set(fixture.Now); }
        Assert.Equal(1, deleted);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, shortProof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, shortProof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>A committed watermark prevents a second root from admitting a fresh proof beyond the original rollback tolerance.</summary>
    private static async Task ClockRollbackAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var advanced = fixture.Now.AddSeconds(WatermarkAdvance);
        fixture.First.GetRequiredService<NativeConsumptionClock>().Set(advanced);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
        fixture.Second.GetRequiredService<NativeConsumptionClock>().Set(advanced.AddSeconds(-OutsideRollback));
        var proof = fixture.Proof();
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.First, proof.Digest, token));
        fixture.Second.GetRequiredService<NativeConsumptionClock>().Set(advanced);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.Second, proof.Text, token);
    }

    /// <summary>Capacity equality denies new proofs, retention equality is not reclaimed, and the next second reclaims at most the exact old batch.</summary>
    private static async Task CapacityAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var now = fixture.Now.AddSeconds(WatermarkAdvance).ToUnixTimeSeconds();
        await NativeAssertionLedger.FillAsync(fixture.First, now, token);
        Assert.Equal(Capacity, await CleanupAssertionFixture.CountAsync(fixture.Second, token));
        var denied = fixture.Proof();
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, denied.Text, token);
        Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.First, denied.Digest, token));
        await MarkRetentionEqualityAsync(fixture, now, token);
        var before = await DigestsAsync(fixture.Second, token);
        Assert.Equal(Capacity, before.Length);
        await CleanupAssertionEligibility.DeniedAsync(fixture.First, fixture.Proof().Text, token);
        Assert.Equal(before, await DigestsAsync(fixture.Second, token));
        fixture.First.GetRequiredService<NativeConsumptionClock>().Set(DateTimeOffset.FromUnixTimeSeconds(now + 1));
        var accepted = fixture.Proof();
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, accepted.Text, token);
        var after = await DigestsAsync(fixture.Second, token);
        Assert.Equal(Capacity - CleanupBatch + 1, after.Length);
        Assert.True(before.Skip(CleanupBatch).Append(accepted.Digest).ToHashSet(StringComparer.Ordinal).SetEquals(after), ExactRetention);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, before[CleanupBatch], token));
    }

    /// <summary>Creates101 synthetic equality tombstones in one owning transaction while leaving all other rows untouched.</summary>
    private static Task<bool> MarkRetentionEqualityAsync(CleanupAssertionFixture fixture, long now, CancellationToken token) =>
        CleanupAssertionFixture.MutateAsync(fixture.First, (scope, current) => scope.Owner.RunAsync(async cancellation =>
        {
            var rows = await scope.Owner.Database.Set<AdministratorClientAssertionUse>().OrderBy(row => row.Digest).Take(ExpiredRows).ToArrayAsync(cancellation);
            foreach (var row in rows)
            {
                row.CreatedAt = now - OldAge;
                row.ExpiresAt = now - RetentionSeconds;
                row.RetainUntil = now;
            }
            await scope.Owner.Database.SaveChangesAsync(cancellation);
        }, current, write: true).AsTask(), token);

    /// <summary>Reads the exact committed retained identities independently, so an equal count cannot hide eviction or substitution.</summary>
    private static Task<string[]> DigestsAsync(IServiceProvider root, CancellationToken token) => CleanupAssertionFixture.ReadAsync(root,
        (database, current) => database.Set<AdministratorClientAssertionUse>().AsNoTracking().OrderBy(row => row.Digest).Select(row => row.Digest).ToArrayAsync(current), token);
}
