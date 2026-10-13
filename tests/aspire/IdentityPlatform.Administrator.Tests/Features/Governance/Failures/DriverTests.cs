using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Checks real child transport independently of native database behavior; provider rows deliberately repeat the same bounded input contract.</summary>
[Collection("administrator-bootstrap")]
public sealed class DriverTests
{
    /// <summary>Repeats eight finite transport inputs under both provider labels without claiming either database is contacted.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Inputs => GovernanceRows.For(DriverTransportCases.Inputs);

    /// <summary>Requires the actual isolated child to reject exact malformed input or cross the input boundary only for the shape-valid control.</summary>
    [Theory, MemberData(nameof(Inputs))]
    public async Task Given_BoundedSyntheticDriverInput_When_TransportIsEvaluated_Then_OnlyFixedFailureAndTerminatedChildRemain(string provider, string condition)
    {
        await using var child = new GovernanceDriver();
        await DriverTransportEvidence.SubmitAsync(child, DriverTransportCases.Create(provider, condition), condition == DriverTransportCases.Oversized);
        await DriverTransportEvidence.RequireAsync(child, condition == DriverTransportCases.Boundary);
    }

    private const string LeaderExited = "leader-exited-descendant-holds-stdout";
    private const string Stubborn = "stubborn-descendant-parent-hard-stop";

    /// <summary>Repeats fixed Linux process-containment controls under provider labels without claiming native provider work.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Lifetimes => GovernanceRows.For(LeaderExited, Stubborn);

    /// <summary>Requires bounded EOF failure plus cleanup after leader loss, or exact parent hard-stop cleanup of a TERM-ignoring group.</summary>
    [Theory, MemberData(nameof(Lifetimes))]
    public async Task Given_OwnedDriverDescendant_When_LeaderExitsOrTerminationIsIgnored_Then_BoundedGroupCleanupLeavesNoLiveGeneration(string provider, string condition)
    {
        Assert.True(provider is DriverTransportCases.PostgreSql or DriverTransportCases.SqlServer);
        Assert.True(condition is LeaderExited or Stubborn);
        var mode = condition == LeaderExited ? GovernanceDriverControl.LeaderExit : GovernanceDriverControl.Stubborn;
        await using var child = new GovernanceDriver(mode);
        await child.StartControlAsync();
        if (condition == LeaderExited)
        {
            await child.ReleaseControlLeaderAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => child.RequireExitAsync(0));
        }
        else
        {
            await child.RequireStubbornAsync();
            await child.RequireHardStopAsync();
        }
        await child.DisposeAsync();
        child.RequireAllTerminated();
    }

}
