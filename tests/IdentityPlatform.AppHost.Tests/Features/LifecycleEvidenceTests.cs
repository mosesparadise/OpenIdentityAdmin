using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class LifecycleEvidenceTests
{
    [Theory]
    [InlineData("Running")]
    [InlineData("Starting")]
    [InlineData(null)]
    public void Missing_pid_cannot_prove_cleanup_of_a_potentially_running_project(string? state)
    {
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireProcessId(state, null));
    }

    [Fact]
    public void Running_process_requires_positive_id_and_terminal_state_is_explicit()
    {
        Assert.Equal(1234, LifecycleEvidence.RequireProcessId(KnownResourceStates.Running, 1234));
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireProcessId(KnownResourceStates.Running, 0));
        Assert.Null(LifecycleEvidence.RequireProcessId(KnownResourceStates.Finished, null));
        Assert.Null(LifecycleEvidence.RequireProcessId(KnownResourceStates.NotStarted, null));
    }

    [Fact]
    public async Task Production_composition_is_rejected_before_resources_start()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                ["--environment=Production"], deadline.Token);
        });
        Assert.Contains("This AppHost is for local Development only", error.ToString());
    }
}
