using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Ensures negative-test diagnostics retain causality without leaking arbitrary native response data.</summary>
public sealed class NativeClientEvidenceTests
{
    /// <summary>Only exact known native errors and documentation identifiers survive sanitization.</summary>
    [Theory]
    [InlineData("invalid_request", "https://documentation.openiddict.com/errors/ID2129", "InvalidRequest/ID2129")]
    [InlineData("invalid_token", "https://documentation.openiddict.com/errors/ID2019", "InvalidToken/ID2019")]
    [InlineData("synthetic-secret", "https://documentation.openiddict.com/errors/ID2129?token=synthetic-secret", "unknown/unknown")]
    [InlineData("invalid_request", "https://untrusted.test/ID2129", "InvalidRequest/unknown")]
    [InlineData(null, null, "unknown/unknown")]
    public void Given_NativeError_When_EvidenceIsFormatted_Then_OnlyAllowlistedConstantsAreReturned(string? error, string? uri, string expected) =>
        Assert.Equal(expected, NativeClientRejection.Describe(error, uri));

    /// <summary>The initial observation failure remains primary while both real operation completions are awaited.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_RaceFailure_When_CleanupJoinsOperations_Then_PrimaryFailureIsPreservedAndNoPeerIsAbandoned(bool primary)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = new InvalidOperationException();
        var secondary = new NotSupportedException();
        using var observation = new CancellationTokenSource();
        var cleanup = NativeRaceCleanup.CompleteAsync(release, observation, Task.CompletedTask,
            Task.FromException(secondary), peer.Task, primary ? ExceptionDispatchInfo.Capture(original) : null);
        Assert.True(release.Task.IsCompletedSuccessfully);
        Assert.True(observation.IsCancellationRequested);
        Assert.False(cleanup.IsCompleted);
        peer.SetResult();
        var error = await Record.ExceptionAsync(() => cleanup);
        Assert.Same(primary ? original : (Exception)secondary, error);
        Assert.True(peer.Task.IsCompletedSuccessfully);
    }
    /// <summary>A completed held winner stops peer observation immediately; an original failure is never replaced by a generic phase error.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_HeldWinner_When_ItCompletesBeforeRelease_Then_ObservationStopsWithTheOriginalFailure(bool faulted)
    {
        var failure = new NotSupportedException();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await NativeRaceCleanup.RequirePendingAsync(pending.Task);
        Assert.False(pending.Task.IsCompleted);
        var winner = faulted ? Task.FromException(failure) : Task.CompletedTask;
        var error = await Record.ExceptionAsync(() => NativeRaceCleanup.RequirePendingAsync(winner));
        if (faulted)
        {
            Assert.Same(failure, error);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(error);
        }
        pending.SetResult();
        await pending.Task;
    }

}
