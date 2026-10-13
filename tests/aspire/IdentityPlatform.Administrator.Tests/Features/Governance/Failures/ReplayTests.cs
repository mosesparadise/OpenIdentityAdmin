using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Exercises the same actual TOTP value through independent native ceremony and governance scopes.</summary>
[Collection("administrator-bootstrap")]
public sealed class ReplayTests(BootstrapFixture fixture)
{
    private const string CeremonyFirst = "ceremony-then-governance";
    private const string GovernanceFirst = "governance-then-ceremony";
    private const string Login = "Login";
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(CeremonyFirst, GovernanceFirst);

    /// <summary>The second factory cannot reuse a committed code, issue another session or change the requested native membership.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CommittedNativeTotp_When_ReusedAcrossCeremonyAndGovernance_Then_SharedLedgerDeniesReplay(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        var code = await scenario.UnusedCodeAsync();
        var correlation = CeremonySecrets.Create();
        var interaction = await BeginLoginAsync(scenario, correlation);
        if (condition == CeremonyFirst)
        {
            Assert.NotNull(await CompleteLoginAsync(scenario, interaction, correlation, code));
        }
        else
        {
            Assert.Equal(GovernanceFirst, condition);
            Assert.Equal(GovernanceOutcome.Accepted, await scenario.ExecuteAsync(request, code: code));
        }
        var before = await GovernanceReplayEvidence.ReadAsync(scenario, request);
        var secondStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (condition == CeremonyFirst)
        {
            Assert.Equal(GovernanceOutcome.Denied, await scenario.ExecuteAsync(request, code: code));
        }
        else
        {
            Assert.Null(await CompleteLoginAsync(scenario, interaction, correlation, code));
        }
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, secondStarted, true);
        var after = await GovernanceReplayEvidence.ReadAsync(scenario, request);
        before.RequireOnlyDenial(after);
    }

    /// <summary>Stages a real password-bound login without granting governance or creating an administrator session.</summary>
    private static async Task<string> BeginLoginAsync(GovernanceScenario scenario, string correlation)
    {
        await using var scope = scenario.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<JourneyPassword>().BeginAsync(scenario.Journey.Lease.Username,
            scenario.Password, Login, CeremonySecrets.Create(), correlation, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Interaction);
        return result.Interaction;
    }

    /// <summary>Uses a separate native scope and the actual ceremony completion factory for each factor submission.</summary>
    private static async Task<JourneyCompletion.Grant?> CompleteLoginAsync(GovernanceScenario scenario,
        string interaction, string correlation, string code)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<JourneyCompletion>().CompleteAsync(interaction, correlation,
            Login, code, TestContext.Current.CancellationToken);
    }
}
