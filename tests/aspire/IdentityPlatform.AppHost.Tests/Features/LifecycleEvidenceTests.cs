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
    public void Restart_proof_rejects_noop_stale_generation_and_original_still_alive()
    {
        var started = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var original = (Id: 100, Started: started);
        var replacement = (Id: 101, Started: started.AddSeconds(1));
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireReplacement(original, original, false));
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireReplacement(original, original, true));
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireReplacement(original, replacement, false));
        Assert.ThrowsAny<Exception>(() => LifecycleEvidence.RequireReplacement(original, (0, started), true));
        LifecycleEvidence.RequireReplacement(original, replacement, true);
        LifecycleEvidence.RequireReplacement(original, (original.Id, started.AddSeconds(1)), true);
    }

    [Fact]
    public void Cleanup_inventory_retains_every_replacement_including_reused_process_ids()
    {
        var started = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var original = (Id: 100, Started: started);
        var replacement = (Id: 100, Started: started.AddSeconds(1));
        var evidence = new LifecycleEvidence(["database"], [original]);
        var merged = evidence.Merge(new LifecycleEvidence(["database", "second-database"], [original, replacement]));
        Assert.Equal(2, merged.Containers.Length);
        Assert.Equal(2, merged.Processes.Length);
        Assert.Contains(original, merged.Processes);
        Assert.Contains(replacement, merged.Processes);
    }

    [Fact]
    public async Task Production_composition_is_rejected_before_resources_start()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                ["--environment=Production"], deadline.Token);
        });
        Assert.Contains("This AppHost is for local Development only", error.ToString());
    }
}
