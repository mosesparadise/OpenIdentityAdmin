using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features.SampleConfiguration;

/// <summary>Executes the actual sample resource command and observes native dashboard state without launching Docker.</summary>
[Trait("Category", "Harness")]
public sealed class SampleDashboardTests
{
    private const string Property = "local-demo-sample-setup";
    private const string Foreign = "foreign";
    private const string Changed = "changed";
    private const string Stopped = "stopped";
    private const string Arguments = "arguments";
    private const string Value = "value";
    private const string ForeignRole = "auth";
    private const int ContainerLength = 64;

    /// <summary>Successful protected stdin changes command availability, clears original buffers/fields and rejects a second invocation.</summary>
    [Fact]
    public async Task Given_RunningOwnedClient_When_Configured_Then_CommandIsTerminalAndInputsAreCleared()
    {
        using var fixture = new SampleDashboardFixture();
        await fixture.PublishAsync(fixture.Resource, SampleDashboardFixture.InvocationId, KnownResourceStates.Running);
        Assert.Equal(ResourceCommandState.Enabled, fixture.State());
        var result = await fixture.ExecuteAsync();
        Assert.True(result.Success);
        Assert.Equal(1, fixture.Process.Mutations);
        Assert.All(fixture.Process.Submitted.ToArray(), value => Assert.Equal(0, value));
        Assert.All(fixture.Interaction.LastInputs, field => Assert.Null(field.Value));
        Assert.Equal(ResourceCommandState.Disabled, fixture.State());
        Assert.True(fixture.Notifications.TryGetCurrentState(SampleDashboardFixture.InvocationId, out var current));
        Assert.Equal("Unavailable", current.Snapshot.Properties.Single(value => value.Name == Property).Value);
        Assert.False((await fixture.ExecuteAsync()).Success);
        Assert.Equal(1, fixture.Process.Mutations);
    }

    /// <summary>The callback independently rejects supplied arguments, foreign resource identity and target replacement after prompting.</summary>
    [Theory]
    [InlineData(Foreign)]
    [InlineData(Changed)]
    [InlineData(Stopped)]
    [InlineData(Arguments)]
    public async Task Given_UnsafeInvocation_When_CommandRuns_Then_NoCredentialsReachExec(string scenario)
    {
        using var fixture = new SampleDashboardFixture();
        var resource = scenario == Foreign ? new ContainerResource(ForeignRole) : fixture.Resource;
        await fixture.PublishAsync(resource, SampleDashboardFixture.InvocationId, KnownResourceStates.Running);
        fixture.Interaction.BeforeReturn = _ =>
        {
            if (scenario == Changed)
            {
                fixture.Process.Id = new string('b', ContainerLength);
            }
            if (scenario == Stopped)
            {
                fixture.Process.Running = false;
            }
            return Task.CompletedTask;
        };
        var arguments = scenario == Arguments ? new InteractionInputCollection([new() { Name = Arguments, Value = Value, InputType = InputType.Text }]) : null;
        var result = await fixture.ExecuteAsync(arguments: arguments);
        Assert.False(result.Success);
        Assert.Equal(0, fixture.Process.Mutations);
        Assert.All(fixture.Interaction.LastInputs, field => Assert.Null(field.Value));
    }

    /// <summary>An uncertain exec stops only the originally pinned owned target and remains terminal even when stop cannot be confirmed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UncertainExec_When_CleanupRuns_Then_OriginalTargetIsStoppedOrFiniteCleanupFailureIsReported(bool cleanupFails)
    {
        using var fixture = new SampleDashboardFixture();
        fixture.Process.FailMutation = true;
        fixture.Process.FailCleanup = cleanupFails;
        await fixture.PublishAsync(fixture.Resource, SampleDashboardFixture.InvocationId, KnownResourceStates.Running);
        var result = await fixture.ExecuteAsync();
        Assert.False(result.Success);
        Assert.Equal(1, fixture.Process.Stops);
        Assert.Equal(cleanupFails, fixture.Process.Running);
        Assert.Equal(ResourceCommandState.Disabled, fixture.State());
        Assert.DoesNotContain(SampleInteraction.Secret, result.Message!, StringComparison.Ordinal);
        Assert.All(fixture.Process.Submitted.ToArray(), value => Assert.Equal(0, value));
        Assert.All(fixture.Interaction.LastInputs, field => Assert.Null(field.Value));
        Assert.False((await fixture.ExecuteAsync()).Success);
        Assert.Equal(1, fixture.Process.Mutations);
    }

    /// <summary>Host cancellation during remote execution uses a fresh cleanup budget and prevents concurrent duplicate submission.</summary>
    [Fact]
    public async Task Given_InFlightExec_When_AppHostStops_Then_ConcurrentCommandIsDeniedAndCleanupStillRuns()
    {
        using var fixture = new SampleDashboardFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Process.OnMutation = async token => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); };
        await fixture.PublishAsync(fixture.Resource, SampleDashboardFixture.InvocationId, KnownResourceStates.Running);
        var first = fixture.ExecuteAsync();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False((await fixture.ExecuteAsync()).Success);
        fixture.StopHost();
        Assert.False((await first).Success);
        Assert.Equal(1, fixture.Process.Mutations);
        Assert.Equal(1, fixture.Process.Stops);
        Assert.False(fixture.Process.Running);
    }
}
