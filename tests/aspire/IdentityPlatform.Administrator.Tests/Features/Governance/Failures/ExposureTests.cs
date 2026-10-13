using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Owns F13's native provider controls; mandatory Host/Contracts tests independently enumerate all actual mapped production routes.</summary>
/// <remarks>Acceptance requires this method on both providers and all four ProductionRoutesTests rows; this method alone does not enumerate remote routes.</remarks>
[Collection("administrator-bootstrap")]
public sealed class ExposureTests
{
    private const string ClosedSurfaces = "closed-surfaces";

    /// <summary>Names the exact approved provider rows for genuine HTTP and pilot-session separation evidence.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(ClosedSurfaces);

    /// <summary>Exercises genuine pilot-cookie denial and direct requests on all three real hosts, retaining private proof construction.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_InternalGovernance_When_ProductionHostsStart_Then_NoManagementOrPilotAuthorityIsMapped(string provider, string condition)
    {
        Assert.Equal(ClosedSurfaces, condition);
        GovernanceExposure.RequirePrivateCapabilities();
        await GovernanceExposure.RequireRealHostsAsync(provider);
    }
}
