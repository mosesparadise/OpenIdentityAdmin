using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class LifecycleEvidenceTests
{
    [Fact]
    public async Task Cleanup_retains_primary_and_multiple_failures_attempts_remaining_actions_and_redacts_messages()
    {
        const string canary = "synthetic-lifecycle-secret";
        var failures = new LifecycleFailures();
        var primary = new InvalidOperationException(canary);
        primary.Data["secret"] = canary;
        failures.Record("stop.peer", primary);
        var attempted = 0;
        await failures.AttemptAsync("cleanup.stop", () => { attempted++; throw new IOException(canary); });
        await failures.AttemptAsync("cleanup.dispose", () => { attempted++; throw new TimeoutException(canary); });
        await failures.AttemptAsync("cleanup.last", () => { attempted++; return Task.CompletedTask; });
        var error = Assert.ThrowsAny<Exception>(failures.RequireSuccess);
        Assert.Equal(3, attempted);
        Assert.False(error.ToString().Contains(canary, StringComparison.Ordinal), "Failure diagnostics exposed exception content.");
        Assert.True(error.InnerException is null, "Failure diagnostics retained an inner exception.");
        Assert.True(error.Data.Count == 0, "Failure diagnostics retained exception metadata.");
        Assert.True(error.Message.Contains("stop.peer:InvalidOperationException", StringComparison.Ordinal), "Primary failure stage was lost.");
        Assert.True(error.Message.Contains("cleanup.stop:IOException", StringComparison.Ordinal), "Stop cleanup stage was lost.");
        Assert.True(error.Message.Contains("cleanup.dispose:TimeoutException", StringComparison.Ordinal), "Dispose cleanup stage was lost.");
        new LifecycleFailures().RequireSuccess();
    }

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
        Assert.False(LifecycleEvidence.HasFreshHealth(started, started.AddSeconds(-1), true));
        Assert.False(LifecycleEvidence.HasFreshHealth(started, null, true));
        Assert.False(LifecycleEvidence.HasFreshHealth(started, started.AddSeconds(1), false));
        Assert.True(LifecycleEvidence.HasFreshHealth(started, started.AddSeconds(1), true));
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
        var child = new ProcessGeneration(200, 10);
        var replacementChild = new ProcessGeneration(201, 20);
        evidence = evidence with
        {
            Descendants = [child]
        };
        var merged = evidence.Merge(new LifecycleEvidence(["database", "second-database"], [original, replacement], [replacementChild]));
        Assert.True(merged.Descendants!.ToHashSet().SetEquals([child, replacementChild]));
        Assert.False(ProcessTreeEvidence.Terminated(child, new(child, 1, 100, 'S')), "An orphaned live child cannot count as terminated.");
        Assert.True(ProcessTreeEvidence.Terminated(child, new(child, 1, 100, 'Z')));
        Assert.True(ProcessTreeEvidence.Terminated(child, new(new(child.Id, child.Started + 1), 1, 100, 'S')));
        Assert.True(ProcessTreeEvidence.Terminated(child, null));
        var parsed = ProcessTreeEvidence.Parse(200, "200 (name with ) parentheses) S 100 100 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 10");
        Assert.Equal(child, parsed.Generation);
        Assert.Equal(100, parsed.Parent);
        Assert.ThrowsAny<Exception>(() => ProcessTreeEvidence.Parse(200, "malformed"));
        var recorder = new LifecycleEvidenceRecorder();
        Parallel.For(1, 65, id => recorder.Add(new LifecycleEvidence([], [], [new(id, (ulong)id)])));
        Assert.True(recorder.Snapshot!.Descendants!.ToHashSet().SetEquals(Enumerable.Range(1, 64).Select(id => new ProcessGeneration(id, (ulong)id))),
            "Concurrent captures lost a process generation.");
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
