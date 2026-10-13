using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Rejects malformed intent before any discovery, credential verification or provider operation can start.</summary>
[Collection("administrator-bootstrap")]
public sealed class RequestTests(BootstrapFixture fixture)
{
    private const string Unsupported = "unsupported-action";
    private const string Contradictory = "contradictory-target-fields";
    private const string Noncanonical = "noncanonical-identifier";
    private const string InvalidVersion = "invalid-composite-version";
    private const string IdentifierPadding = " ";
    private const int UnsupportedActionValue = int.MaxValue;
    private const int InvalidRevision = -1;
    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        GovernanceRows.For(Unsupported, Contradictory, Noncanonical, InvalidVersion);

    /// <summary>A real native actor supplies coherent original intent, while each independent invalid shape produces no request or durable side effect.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_InvalidCommandShape_When_Validated_Then_NoDiscoveryOrNativeWriteStarts(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var valid = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        var before = await scenario.SnapshotAsync();
        var invalid = condition switch
        {
            Unsupported => GovernanceRequest.Create((GovernanceAction)UnsupportedActionValue, valid.Versions, valid.UserId, valid.RoleId),
            Contradictory => GovernanceRequest.Create(GovernanceAction.AddGrant, valid.Versions, valid.UserId, valid.RoleId, GovernanceCommands.Permission),
            Noncanonical => GovernanceRequest.Create(valid.Action, valid.Versions, valid.UserId + IdentifierPadding, valid.RoleId),
            InvalidVersion => GovernanceRequest.Create(valid.Action, valid.Versions with { EnrollmentRevision = InvalidRevision }, valid.UserId, valid.RoleId),
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };
        Assert.Null(invalid);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
