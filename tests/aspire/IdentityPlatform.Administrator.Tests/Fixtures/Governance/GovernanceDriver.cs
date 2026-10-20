using IdentityPlatform.Administrator.Tests.Fixtures.Processes;
using IdentityPlatform.Core.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Applies governance command result semantics over the shared bounded process-group transport.</summary>
internal sealed class GovernanceDriver(GovernanceDriverControl control = GovernanceDriverControl.None) : DriverProcess(control, DriverExecutable.Governance)
{
    private const string Phase = "phase";
    private const string Outcome = "outcome";
    private const string UnexpectedOutput = "The governance driver produced an unexpected frame; raw output is withheld.";
    private const int UnavailableExit = 2;
    /// <summary>Consumes only valid post-readiness command framing; unavailable completion makes no rollback assertion.</summary>
    internal async Task<GovernanceOutcome?> CompleteCommandAsync()
    {
        using var first = await ReadFrameAsync();
        var phase = first.RootElement.GetProperty(Phase).GetString();
        if (phase == Unavailable)
        {
            await RequireExitAsync(UnavailableExit);
            return null;
        }
        if (phase == Committed)
        {
            using var final = await ReadFrameAsync(Result);
            Assert.True(final.RootElement.TryGetProperty(Outcome, out var outcome) &&
                outcome.GetString() == nameof(GovernanceOutcome.Accepted), UnexpectedOutput);
            await RequireExitAsync(0);
            return GovernanceOutcome.Accepted;
        }
        Assert.True(phase == Result && first.RootElement.TryGetProperty(Outcome, out _), UnexpectedOutput);
        var text = first.RootElement.GetProperty(Outcome).GetString();
        Assert.True(text is nameof(GovernanceOutcome.Denied) or nameof(GovernanceOutcome.NoChange), UnexpectedOutput);
        await RequireExitAsync(0);
        return Enum.Parse<GovernanceOutcome>(text!);
    }

}
