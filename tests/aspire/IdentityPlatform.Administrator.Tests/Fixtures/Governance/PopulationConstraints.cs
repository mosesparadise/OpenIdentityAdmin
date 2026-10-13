using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Credits actual provider rejection of impossible malformed rows without pretending a policy read consumed them.</summary>
internal static class PopulationConstraints
{
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const int SqlConstraintViolation = 547;
    private const string PostgreSqlCheck = "23514";
    private const string PostgreSqlRestrict = "23001";
    private const string EnrollmentRevision = "CK_AdministratorEnrollment_Revision";
    private const string AuthenticationState = "CK_AdministratorAuthentication_State";
    private const string ExpectedRejection = "Expected a native check/foreign-key constraint rejection; raw provider diagnostics are withheld.";
    private const string ConstraintCredit = "population-proof=provider-constraint-rejection; policy-read-of-corrupt-state=not-executed";

    /// <summary>Requires the specific provider rejection family and unchanged durable snapshot, then verifies coherent positive state remains usable.</summary>
    internal static async Task RequireRejectedAsync(GovernanceScenario scenario, string provider, string condition)
    {
        var before = await scenario.SnapshotAsync();
        var failure = await Record.ExceptionAsync(() => PopulationDamage.ApplyAsync(scenario, condition));
        var foreignKey = condition is PopulationCases.MissingEnrollment or PopulationCases.MissingAuthentication;
        var expectedName = condition == PopulationCases.NegativeEnrollmentRevision ? EnrollmentRevision : AuthenticationState;
        var native = failure is DbUpdateException update ? update.InnerException : failure;
        var accepted = provider switch
        {
            PostgreSql => native is PostgresException postgres &&
                postgres.SqlState == (foreignKey ? PostgreSqlRestrict : PostgreSqlCheck) &&
                (foreignKey || postgres.ConstraintName == expectedName),
            SqlServer => native is SqlException sql && sql.Number == SqlConstraintViolation &&
                (foreignKey || sql.Message.Contains(expectedName, StringComparison.Ordinal)),
            _ => false
        };
        Assert.True(accepted, $"{ExpectedRejection} {Describe(native)}");
        Assert.Equal(before, await scenario.SnapshotAsync());
        await PopulationEvidence.RequireCompleteAsync(scenario);
        TestContext.Current.TestOutputHelper!.WriteLine(ConstraintCredit);
    }
    /// <summary>Reports only exception family and fixed provider error codes, excluding diagnostic text and constraint data.</summary>
    private static string Describe(Exception? failure) => failure switch
    {
        PostgresException postgres => $"{nameof(PostgresException)}:{postgres.SqlState}",
        SqlException sql => $"{nameof(SqlException)}:{sql.Number}",
        null => nameof(Exception),
        _ => failure.GetType().Name
    };
}
