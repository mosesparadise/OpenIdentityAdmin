using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires final exact persistence verification to roll back terminal policy, newly staged approval/job, receipt and both audits together.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementAtomicityTests(BootstrapFixture fixture)
{
    /// <summary>A genuine post-save policy or receipt corruption cannot commit; the original version remains usable after independently observed rollback.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql", "CriticalDecision", "retirement-policy-persistence"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql", "CriticalDecision", "retirement-receipt-persistence"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer", "CriticalDecision", "retirement-policy-persistence"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer", "CriticalDecision", "retirement-receipt-persistence"])]
    public async Task Given_PersistedRetirement_When_FinalPolicyOrReceiptIsCorrupted_Then_NoTerminalStateGrantApprovalOrHistoryCommits(string provider, bool receipt)
    {
        var fault = new ApplicationRetirementSaveFault(receipt);
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider, fault);
        await test.Grants.SeedAsync(test.Id, 1, 1);
        var before = await test.SnapshotAsync();
        var intent = test.Intent();
        fault.Armed = true;
        await ApplicationRetirementCausalWitness.AtomicAsync(test, intent, fault, receipt);
        Assert.True(fault.Fired);
        Assert.Equal(before, await test.SnapshotAsync());
        var accepted = await test.RetireAsync(intent);
        Assert.Equal(test.Id, accepted.ApplicationId);
        Assert.Equal(Guid.Parse(accepted.Job.Id), (await test.ReceiptAsync()).GrantJobId);
    }
}
