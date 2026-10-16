using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Tests original deadlines, exact ownership and terminal failure through the actual opt-in owner.</summary>
public sealed class StorageOwnershipTests
{
    /// <summary>Rejects equality and late completion even when the native operation already has a completed result.</summary>
    [Theory]
    [InlineData(9999, true)]
    [InlineData(10000, false)]
    [InlineData(10001, false)]
    public async Task Given_CompletedNativeCall_When_OriginalDeadlineElapses_Then_ExactBoundaryIsEnforced(int milliseconds, bool accepted)
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        // Advance time inside a completed native call to exercise the post-call deadline check.
        ValueTask<int> Complete(CancellationToken token)
        {
            probe.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            const int NativeValue = 42;
            return new(NativeValue);
        }
        if (accepted)
        {
            Assert.Equal(42, await scope.Owner.RunAsync(Complete, CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.Owner.RunAsync(Complete, CancellationToken.None).AsTask());
            Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
        }
    }

    /// <summary>Starts the phase from the remaining original budget and refuses a source captured before the phase change.</summary>
    [Fact]
    public async Task Given_NineSecondOldRequest_When_StartingPhase_Then_OneSecondRemainsAndOldSourceFaults()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        const string UnexpectedExecution = "must not execute";
        const int RequestAgeSeconds = 9;
        var source = scope.Owner.Enumerate<int>(_ => throw new InvalidOperationException(UnexpectedExecution), CancellationToken.None);
        probe.Clock.Advance(TimeSpan.FromSeconds(RequestAgeSeconds));
        await using var transaction = scope.Owner.CreateTransaction();
        Assert.Equal(TimeSpan.FromSeconds(1), scope.Owner.Capture().Lifetime.Remaining);
        Assert.Throws<InvalidOperationException>(() => source.GetAsyncEnumerator(TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }

    /// <summary>Rejects writes without an explicit transaction and does not permit recovery by catching the rejection.</summary>
    [Fact]
    public async Task Given_ReadOnlyScope_When_WriteIsAttempted_Then_OwnerBecomesTerminal()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture(write: true));
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }

    /// <summary>Rejects a substituted context while genuine owned execution is active.</summary>
    [Fact]
    public async Task Given_ActiveExecution_When_ContextIsSubstituted_Then_OwnerBecomesTerminal()
    {
        await using var probe = new StorageProbe();
        await using var other = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Owner.RunAsync<int>(_ =>
        {
            scope.Owner.RequireContext(other.Database);
            return new(1);
        }, CancellationToken.None).AsTask());
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }

    /// <summary>Preserves a fault even when a caller catches native cancellation and tries another operation.</summary>
    [Fact]
    public async Task Given_CancelledCaller_When_NativeCallCompletes_Then_FaultRemainsLatched()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        using var caller = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.Owner.RunAsync<int>(_ =>
        {
            caller.Cancel();
            return new(1);
        }, caller.Token).AsTask());
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }

    /// <summary>Rejects broad maintenance even if the caller catches its unsupported-operation exception.</summary>
    [Fact]
    public async Task Given_BroadPrune_When_Rejected_Then_OwnerBecomesTerminal()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<NotSupportedException>(() => scope.Owner.RejectPrune().AsTask());
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }
}
