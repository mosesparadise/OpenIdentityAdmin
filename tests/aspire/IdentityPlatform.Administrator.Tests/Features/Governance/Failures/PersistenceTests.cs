using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Exercises real provider numeric constraints and checked governance transitions without dropping production guards.</summary>
[Collection("administrator-bootstrap")]
public sealed class PersistenceTests(BootstrapFixture fixture)
{
    private const string UpgradeCase = "upgrade-and-readiness";
    private const string RuntimeCase = "runtime-privileges";
    private const string Negative = "negative-authority-constraint";
    private const string Authority = "authority-revision-overflow";
    private const string Governance = "technical-governance-revision-overflow";
    private const string Authentication = "authentication-revision-overflow";
    private const string Generation = "authentication-generation-overflow";
    private const string Enrollment = "enrollment-revision-overflow";
    private const int InvalidRevision = -1;
    private const int SqlConstraintViolation = 547;
    private const string AuthorityConstraint = "CK_AdministratorGovernance_AuthorityRevision";
    private const string ExpectedConstraint = "Expected the native nonnegative authority constraint; provider diagnostics are withheld.";
    public static IEnumerable<TheoryDataRow<string, string>> NumericCases =>
        GovernanceRows.For(Negative, Authority, Governance, Authentication, Generation, Enrollment);

    /// <summary>Every boundary either rejects impossible persisted state or rolls back the complete attempted command, never wrapping or consuming proof.</summary>
    [Theory, MemberData(nameof(NumericCases))]
    public async Task Given_InvalidSemanticVersion_When_StorageOrCommandChangesIt_Then_NoWraparoundCanCommit(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        if (condition == Negative)
        {
            await RequireNegativeRejectedAsync(scenario);
            return;
        }
        var target = await GovernanceAccounts.AddAsync(scenario);
        await SeedMaximumAsync(scenario, target, condition);
        var action = condition == Enrollment ? GovernanceAction.SuspendAccount : GovernanceAction.DisableAccount;
        var request = await scenario.RequestAsync(action, target);
        var before = await scenario.SnapshotAsync();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(request));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Attempts a negative semantic revision through native persistence and requires its exact check family.</summary>
    private static async Task RequireNegativeRejectedAsync(GovernanceScenario scenario)
    {
        var before = await scenario.SnapshotAsync();
        await using (var scope = scenario.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var token = TestContext.Current.CancellationToken;
            var row = await database.Set<AdministratorGovernance>().SingleAsync(token);
            row.AuthorityRevision = InvalidRevision;
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
            Assert.True(failure.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.CheckViolation, ConstraintName: AuthorityConstraint } ||
                failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlConstraintViolation } sql &&
                sql.Message.Contains(AuthorityConstraint, StringComparison.Ordinal), ExpectedConstraint);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
        await PopulationEvidence.RequireCompleteAsync(scenario);
    }

    /// <summary>Sets only the boundary under test; a separate fully usable actor prevents invalid target session bindings from masking checked writes.</summary>
    private static async Task SeedMaximumAsync(GovernanceScenario scenario, string target, string condition)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var governance = await database.Set<AdministratorGovernance>().SingleAsync(token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == target, token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == target, token);
        switch (condition)
        {
            case Authority:
                governance.AuthorityRevision = long.MaxValue;
                break;
            case Governance:
                governance.Revision = long.MaxValue;
                break;
            case Authentication:
                authentication.Revision = long.MaxValue;
                break;
            case Generation:
                authentication.Generation = long.MaxValue;
                break;
            case Enrollment:
                enrollment.Revision = long.MaxValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition));
        }
        await database.SaveChangesAsync(token);
    }
    /// <summary>Names the reviewed provider rows without treating proposed source as executed discovery evidence.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> UpgradeCases => GovernanceRows.For(UpgradeCase);

    /// <summary>Names the real runtime-principal permission experiment on each mandatory provider.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> RuntimeCases => GovernanceRows.For(RuntimeCase);

    /// <summary>Restores the prior layout around genuine populated native state, then proves additive migration and readiness.</summary>
    [Theory, MemberData(nameof(UpgradeCases))]
    public async Task Given_PreGovernanceSchemaAndData_When_Migrated_Then_AdditiveRevisionPreservesExistingAuthority(string provider, string condition)
    {
        Assert.Equal(UpgradeCase, condition);
        await GovernanceUpgrade.RequireAsync(fixture, provider);
    }

    /// <summary>Runs actual governed DML using the runtime principal before requiring native schema alteration to fail.</summary>
    [Theory, MemberData(nameof(RuntimeCases))]
    public async Task Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny(string provider, string condition)
    {
        Assert.Equal(RuntimeCase, condition);
        await GovernancePrivileges.RequireAsync(fixture, provider);
    }
}
