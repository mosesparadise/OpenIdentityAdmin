using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises the real provider's serialized assertion replay ledger independently of pending native authentication integration.</summary>
internal static class NativeAssertionLedger
{
    private const int Lifetime = 60;
    private const int Retention = 30;
    private const int Capacity = 10000;
    private const int ExpiredCount = 101;
    private const int CleanupBatch = 100;
    private const int DigestLength = 64;
    private const int HexBase = 16;
    private const int OldAge = 100;
    private const int SeedBatch = 250;
    private const string IdentifierPrefix = "native-assertion-";

    /// <summary>Proves cross-instance single use, strict expiry, bounded cleanup and exact capacity without eviction.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        var clock = first.GetRequiredService<NativeConsumptionClock>();
        var otherClock = second.GetRequiredService<NativeConsumptionClock>();
        var now = clock.Freeze();
        otherClock.Set(now);
        try
        {
            var proof = Create(now.ToUnixTimeSeconds());
            var outcomes = await Task.WhenAll(ConsumeAsync(first, proof, token), ConsumeAsync(second, proof, token));
            Assert.Single(outcomes, value => value);
            Assert.Single(outcomes, value => !value);
            Assert.Equal(1, await CountAsync(second, token));
            Assert.False(await ConsumeAsync(second, AdministratorAssertion.Create(IdentifierPrefix + Guid.NewGuid().ToString(),
                now.ToUnixTimeSeconds() - 1, now.ToUnixTimeSeconds(), null)!, token));
            await VerifyUnknownCommitAsync(first, second, now.ToUnixTimeSeconds(), token);
            await VerifyLaterFailureAsync(first, second, now.ToUnixTimeSeconds(), token);
            await VerifyCapacityAsync(first, second, now.ToUnixTimeSeconds(), token);
        }
        finally { clock.Reset(); otherClock.Reset(); }
    }

    /// <summary>A lost real commit acknowledgement cannot retry or restore the consumed proof on the independent instance.</summary>
    private static async Task VerifyUnknownCommitAsync(IServiceProvider first, IServiceProvider second, long now, CancellationToken token)
    {
        var proof = Create(now);
        var expected = new ArithmeticException();
        var commits = 0;
        first.GetRequiredService<NativeConsumptionCommit>().AfterCommit = () => { commits++; throw expected; };
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => ConsumeAsync(first, proof, token)));
        }
        finally { first.GetRequiredService<NativeConsumptionCommit>().AfterCommit = null; }
        Assert.Equal(1, commits);
        Assert.False(await ConsumeAsync(second, proof, token));
    }

    /// <summary>A later failed write phase in the same original request cannot roll back an earlier independent ledger commit.</summary>
    private static async Task VerifyLaterFailureAsync(IServiceProvider first, IServiceProvider second, long now, CancellationToken token)
    {
        var proof = Create(now);
        var expected = new ArithmeticException();
        var factory = first.GetRequiredService<IServiceScopeFactory>();
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => new ProtocolStorageRequest(first.GetRequiredService<TimeProvider>(), token)
            .RunAsync(factory, async (_, _) =>
            {
                Assert.True(await ProtocolStorageChild.RunAsync(factory, async (child, cancellation) =>
                {
                    await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                    var accepted = await AdministratorAssertionLedger.ReserveAsync(child.Owner, proof, cancellation);
                    await transaction.CommitAsync(cancellation);
                    return accepted;
                }));
                return await ProtocolStorageChild.RunAsync<bool>(factory, async (child, cancellation) =>
                {
                    await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                    await AdministratorProtocolLocks.AcquireProfileAsync(child.Owner, cancellation);
                    throw expected;
                });
            })));
        Assert.False(await ConsumeAsync(second, proof, token));
    }

    /// <summary>Capacity equality denies a new digest; cleanup retains equality tombstones and deletes only one bounded batch afterward.</summary>
    private static async Task VerifyCapacityAsync(IServiceProvider first, IServiceProvider second, long now, CancellationToken token)
    {
        await FillAsync(first, now, token);
        Assert.False(await ConsumeAsync(second, Create(now), token));
        Assert.Equal(Capacity, await CountAsync(first, token));
        await MutateAsync(first, async (database, cancellation) =>
        {
            var rows = await database.Set<AdministratorClientAssertionUse>().OrderBy(row => row.Digest).Take(ExpiredCount).ToArrayAsync(cancellation);
            foreach (var row in rows)
            {
                row.CreatedAt = now - OldAge;
                row.ExpiresAt = now - Retention;
                row.RetainUntil = now;
            }
            await database.SaveChangesAsync(cancellation);
        }, token);
        Assert.False(await ConsumeAsync(second, Create(now), token));
        Assert.Equal(Capacity, await CountAsync(first, token));
        second.GetRequiredService<NativeConsumptionClock>().Set(DateTimeOffset.FromUnixTimeSeconds(now + 1));
        Assert.True(await ConsumeAsync(second, Create(now), token));
        Assert.Equal(Capacity - CleanupBatch + 1, await CountAsync(first, token));
    }

    /// <summary>Seeds the exact capacity through bounded independent fixture transactions without extending any write-phase deadline.</summary>
    internal static async Task FillAsync(IServiceProvider root, long now, CancellationToken token)
    {
        var count = await CountAsync(root, token);
        while (count < Capacity)
        {
            var start = count;
            var size = Math.Min(SeedBatch, Capacity - count);
            await MutateAsync(root, async (database, cancellation) =>
            {
                database.Set<AdministratorClientAssertionUse>().AddRange(Enumerable.Range(start, size).Select(index => Row(index, now)));
                await database.SaveChangesAsync(cancellation);
            }, token);
            count += size;
        }
    }

    /// <summary>Commits only accepted consumption; a denial rolls back speculative cleanup and grants no signature authority.</summary>
    private static Task<bool> ConsumeAsync(IServiceProvider root, AdministratorAssertion proof, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var accepted = await AdministratorAssertionLedger.ReserveAsync(scope.Owner, proof, cancellation);
            if (accepted)
            {
                await transaction.CommitAsync(cancellation);
            }
            return accepted;
        });

    /// <summary>Observes committed retained-row count through an independent owner context.</summary>
    private static Task<int> CountAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
            (scope, cancellation) => scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<AdministratorClientAssertionUse>().CountAsync(current)), cancellation));

    /// <summary>Seeds synthetic capacity states only inside a fresh explicit transaction holding the same profile lock.</summary>
    private static Task<bool> MutateAsync(IServiceProvider root, Func<DbContext, CancellationToken, Task> mutate, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            await scope.Owner.RunAsync(current => new ValueTask(mutate(scope.Owner.Database, current)), cancellation, write: true);
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Creates a fresh bounded synthetic digest input for each intentional reservation attempt.</summary>
    private static AdministratorAssertion Create(long now) => AdministratorAssertion.Create(IdentifierPrefix + Guid.NewGuid().ToString(), now, now + Lifetime, null)!;

    /// <summary>Supplies canonical synthetic rows whose timestamps satisfy the actual provider constraints.</summary>
    private static AdministratorClientAssertionUse Row(int index, long now) => new()
    {
        Digest = Convert.ToString(index, HexBase).PadLeft(DigestLength, '0'),
        CreatedAt = now,
        ExpiresAt = now + Lifetime,
        RetainUntil = now + Lifetime + Retention
    };
}
