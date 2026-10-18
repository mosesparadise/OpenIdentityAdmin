using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Reconciles actual Sessions rows at hard limits, across instances and after bounded selective cleanup.</summary>
internal static class NativeClientStateQuotas
{
    private const int LiveBefore = 1023;
    private const int LiveLimit = 1024;
    private const int RetainedBefore = 9999;
    private const int RetainedLimit = 10000;
    private const int OldStates = 101;
    private const int AfterFirstCleanup = 4;
    private const int AfterSecondCleanup = 3;
    private const string Unavailable = "Native protocol storage ownership is unavailable.";

    /// <summary>Uses two separately composed clients and one shared least-privilege database for independent race outcomes.</summary>
    internal static async Task VerifyAsync(NativeProviderFixture fixture, NativeLockObservation observer, CancellationToken token)
    {
        await using var first = NativeClientStateFixture.Create(fixture, new());
        await using var second = NativeClientStateFixture.Create(fixture, new());
        await LiveRaceAsync(first, second, observer, token);
        await RetainedAsync(first, token);
        await CleanupAsync(first, token);
        await NativeClientStateFixture.ClearAsync(first, token);
    }

    /// <summary>Exactly one of two concurrent native creations can occupy the final live state slot.</summary>
    private static async Task LiveRaceAsync(IServiceProvider first, IServiceProvider second, NativeLockObservation observer, CancellationToken token)
    {
        await NativeClientStateFixture.ClearAsync(first, token);
        await NativeClientStateQuotaFixture.FillAsync(first, LiveBefore, NativeClientStateQuotaFixture.Kind.Live, token);
        await NativeClientStateRace.VerifyAsync(first, second, observer, token);
        Assert.Equal(LiveLimit, await NativeClientStateFixture.CountAsync(second, token));
        Assert.False(await AttemptAsync(first, token));
        Assert.Equal(LiveLimit, await NativeClientStateFixture.CountAsync(second, token));
    }

    /// <summary>Consumed but retained state still occupies the total cap; equality is accepted only for the last permitted creation.</summary>
    private static async Task RetainedAsync(IServiceProvider root, CancellationToken token)
    {
        await NativeClientStateFixture.ClearAsync(root, token);
        await NativeClientStateQuotaFixture.FillAsync(root, RetainedBefore, NativeClientStateQuotaFixture.Kind.Retained, token);
        Assert.True(await AttemptAsync(root, token));
        Assert.Equal(RetainedLimit, await NativeClientStateFixture.CountAsync(root, token));
        Assert.False(await AttemptAsync(root, token));
        Assert.Equal(RetainedLimit, await NativeClientStateFixture.CountAsync(root, token));
    }

    /// <summary>Mixed native type casing behaves identically on both engines; unrelated rows survive every bounded batch.</summary>
    private static async Task CleanupAsync(IServiceProvider root, CancellationToken token)
    {
        await NativeClientStateFixture.ClearAsync(root, token);
        await NativeClientStateQuotaFixture.FillAsync(root, OldStates, NativeClientStateQuotaFixture.Kind.ExpiredMixedCase, token);
        await NativeClientStateQuotaFixture.FillAsync(root, 1, NativeClientStateQuotaFixture.Kind.Foreign, token);
        Assert.True(await AttemptAsync(root, token));
        Assert.Equal(AfterFirstCleanup, await NativeClientStateFixture.CountAsync(root, token));
        Assert.True(await NativeClientStateQuotaFixture.HasForeignAsync(root, token));
        Assert.True(await AttemptAsync(root, token));
        Assert.Equal(AfterSecondCleanup, await NativeClientStateFixture.CountAsync(root, token));
        Assert.True(await NativeClientStateQuotaFixture.HasForeignAsync(root, token));
    }

    /// <summary>Accepts only the fixed ownership denial; database, timeout or unexpected native failures remain failing evidence.</summary>
    internal static async Task<bool> AttemptAsync(IServiceProvider root, CancellationToken token)
    {
        try
        {
            return !string.IsNullOrEmpty(await NativeClientStateFixture.GenerateAsync(root, token));
        }
        catch (InvalidOperationException error) when (error.Message == Unavailable)
        {
            return false;
        }
    }
}
