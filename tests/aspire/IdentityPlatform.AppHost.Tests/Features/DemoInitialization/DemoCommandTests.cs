using IdentityPlatform.AppHost.Features.LocalDemo;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Protects admission, cancellation, secrecy and terminal handling around native initialization.</summary>
[Trait("Category", "Harness")]
public sealed class DemoCommandTests
{
    private const string NotReady = "not-ready";
    private const string ForeignRun = "foreign-run";
    private const string WrongRole = "wrong-role";
    private const string Stopped = "stopped";
    private const string ChangedId = "changed-id";
    private const string LostReadiness = "lost-readiness";
    private const string ForeignRole = "auth";
    private const string CleanupFailure = "cleanup could not be confirmed";
    private const int RunLength = 32;
    private const int ContainerLength = 64;
    private const int InvalidUsernameLength = 129;
    private const int InvalidPasswordLength = 257;

    /// <summary>Unavailable or canceled interactions cannot create an account and remain safely retryable.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Given_UnavailableOrCanceledDialog_When_Invoked_Then_NoMutationOccurs(bool available, bool cancel)
    {
        var process = new DemoProcessWitness();
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        var interaction = new DemoInteraction { IsAvailable = available, Cancel = cancel };
        var result = await command.ExecuteAsync(interaction, () => true, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(0, process.Mutations);
        Assert.True(command.Available);
        Assert.All(interaction.LastInputs, field => Assert.True(string.IsNullOrEmpty(field.Value)));
    }

    /// <summary>Server-side validation rejects malformed values despite a successful dashboard response.</summary>
    [Theory]
    [InlineData("bad name", "Aa1!synthetic-canary-password")]
    [InlineData("synthetic-demo-admin", "short")]
    [InlineData("", "Aa1!synthetic-canary-password")]
    public async Task Given_InvalidReturnedInput_When_Submitted_Then_NoMutationOrSecretDisclosureOccurs(string user, string password)
    {
        var process = new DemoProcessWitness();
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        var interaction = new DemoInteraction { Username = user, Password = password };
        var result = await command.ExecuteAsync(interaction, () => true, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(0, process.Mutations);
        Assert.False(result.Message?.Contains(DemoInteraction.Secret, StringComparison.Ordinal) == true);
        Assert.All(interaction.LastInputs, field => Assert.True(string.IsNullOrEmpty(field.Value)));
        Assert.True(command.Available);
    }

    /// <summary>Model readiness and safe Docker fields are checked independently of presentation state.</summary>
    [Theory]
    [InlineData(NotReady)]
    [InlineData(ForeignRun)]
    [InlineData(WrongRole)]
    [InlineData(Stopped)]
    [InlineData(ChangedId)]
    [InlineData(LostReadiness)]
    public async Task Given_StaleOrForeignInitializer_When_Invoked_Then_MutationIsDenied(string condition)
    {
        var process = new DemoProcessWitness();
        var interaction = new DemoInteraction();
        var running = condition != NotReady;
        if (condition == ForeignRun)
        {
            process.Label = new string('f', RunLength);
        }
        if (condition == WrongRole)
        {
            process.Role = ForeignRole;
        }
        if (condition == Stopped)
        {
            process.Running = false;
        }
        interaction.BeforeReturn = _ =>
        {
            if (condition == ChangedId)
            {
                process.Id = new string('b', ContainerLength);
            }
            if (condition == LostReadiness)
            {
                running = false;
            }
            return Task.CompletedTask;
        };
        var result = await new DemoCommand(process, DemoProcessWitness.Run).ExecuteAsync(interaction, () => running, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(0, process.Mutations);
    }

    /// <summary>Length metadata cannot be bypassed by a caller returning oversized values.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_OversizedReturnedField_When_Submitted_Then_NoAccountMutationOccurs(bool username)
    {
        var process = new DemoProcessWitness();
        var interaction = new DemoInteraction();
        if (username)
        {
            interaction.Username = new string('a', InvalidUsernameLength);
        }
        else
        {
            interaction.Password = new string('a', InvalidPasswordLength);
        }
        var result = await new DemoCommand(process, DemoProcessWitness.Run).ExecuteAsync(interaction, () => true, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(0, process.Mutations);
        Assert.All(interaction.LastInputs, field => Assert.True(string.IsNullOrEmpty(field.Value)));
    }

    /// <summary>A successful native sequence consumes the attempt and clears all dashboard secret references.</summary>
    [Fact]
    public async Task Given_ValidInput_When_InitializationCompletes_Then_RepeatInvocationCannotMutate()
    {
        var process = new DemoProcessWitness();
        var interaction = new DemoInteraction();
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        var first = await command.ExecuteAsync(interaction, () => true, TestContext.Current.CancellationToken);
        var second = await command.ExecuteAsync(interaction, () => true, TestContext.Current.CancellationToken);
        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.True(process.ValidBootstrap);
        Assert.Equal(2, process.Mutations);
        Assert.Equal(1, interaction.Prompts);
        Assert.All(interaction.LastInputs, field => Assert.True(string.IsNullOrEmpty(field.Value)));
        Assert.False(first.Message?.Contains(DemoInteraction.Secret, StringComparison.Ordinal) == true);
    }

    /// <summary>Partial success or lost acknowledgement is terminal and stops only the exact owned target.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public async Task Given_NativeFailure_When_MutationMayHaveStarted_Then_OwnedCleanupAndTerminalDenialApply(int failAt, bool cleanupFails)
    {
        var process = new DemoProcessWitness { FailAt = failAt, FailCleanup = cleanupFails };
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        var result = await command.ExecuteAsync(new DemoInteraction(), () => true, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(failAt, process.Mutations);
        Assert.Equal(1, process.Stops);
        Assert.False(command.Available);
        Assert.False(result.Message?.Contains(DemoInteraction.Secret, StringComparison.Ordinal) == true);
        Assert.Equal(cleanupFails, result.Message!.Contains(CleanupFailure, StringComparison.Ordinal));
        Assert.False((await command.ExecuteAsync(new DemoInteraction(), () => true, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(failAt, process.Mutations);
    }

    /// <summary>Concurrent execution is rejected rather than queued behind a prompt, and pre-submit cancellation releases admission.</summary>
    [Fact]
    public async Task Given_ActivePrompt_When_ConcurrentInvocationAndCancellationOccur_Then_NoMutationIsQueued()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var interaction = new DemoInteraction { BeforeReturn = async token => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); } };
        var process = new DemoProcessWitness();
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        var first = command.ExecuteAsync(interaction, () => true, cancellation.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False((await command.ExecuteAsync(new DemoInteraction(), () => true, TestContext.Current.CancellationToken)).Success);
        await cancellation.CancelAsync();
        Assert.False((await first).Success);
        Assert.Equal(0, process.Mutations);
        Assert.True(command.Available);
    }

    /// <summary>Cancellation after submission uses a fresh cleanup token and permanently consumes this run's attempt.</summary>
    [Fact]
    public async Task Given_SubmittedMutation_When_CancellationOccurs_Then_CleanupOutlivesCanceledOperation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new DemoProcessWitness { OnMutation = async token => { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); } };
        var command = new DemoCommand(process, DemoProcessWitness.Run);
        Assert.False((await command.ExecuteAsync(new DemoInteraction(), () => true, cancellation.Token)).Success);
        Assert.Equal(1, process.Stops);
        Assert.False(process.Running);
        Assert.False(command.Available);
    }
}
