using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Checks actual locked full-session proof and honestly distinguishes database constraints from reachable command denials.</summary>
internal static class GovernanceSessionEvidence
{
    private const string UntruthfulMethods = "pwd sms";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const int SqlServerCheckViolation = 547;
    private const string SessionStateConstraint = "CK_AdministratorSession_State";
    private const string UnexpectedConstraint = "The native session state check was not the observed failure.";

    /// <summary>Uses the real private proof factory under original account locks; tests never construct a session proof.</summary>
    internal static async Task RequireProofAsync(GovernanceScenario scenario, GovernanceRequest request,
        JourneySessionCandidate candidate, bool expected)
    {
        await using var operation = await GovernanceOperation.OpenAsync(scenario, request);
        var proof = await JourneySessionProof.ReadAsync(operation.Get<PlatformDbContext>(), operation.Get<UserManager<PlatformUser>>(),
            operation.Get<CeremonyClock>(), operation.Transaction, operation.State.Actor, candidate);
        Assert.Equal(expected, proof is not null);
    }

    /// <summary>Requires true native schema rejection, unchanged state and an independently exercised read-only method predicate.</summary>
    internal static async Task RequireMethodsConstraintAsync(GovernanceScenario scenario, GovernanceRequest request, string provider)
    {
        var before = await scenario.SnapshotAsync();
        await RejectUntruthfulPersistenceAsync(scenario, provider);
        Assert.Equal(before, await scenario.SnapshotAsync());
        await using (var operation = await GovernanceOperation.OpenAsync(scenario, request))
        {
            var database = operation.Get<PlatformDbContext>();
            var session = await database.Set<AdministratorSession>().AsNoTracking()
                .SingleAsync(row => row.PublicId == scenario.Session.SessionId, operation.Transaction.Token);
            var account = operation.State.Actor;
            var now = operation.Get<CeremonyClock>().Now;
            Assert.True(JourneySessionState.Allows(account.User, account.Enrollment, account.Authentication, session, now));
            session.AuthenticationMethods = UntruthfulMethods;
            Assert.False(JourneySessionState.Allows(account.User, account.Enrollment, account.Authentication, session, now));
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Attempts a seven-character incorrect method value, isolating state validation from the separate length constraint.</summary>
    private static async Task RejectUntruthfulPersistenceAsync(GovernanceScenario scenario, string provider)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var session = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == scenario.Session.SessionId, token);
        session.AuthenticationMethods = UntruthfulMethods;
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
        if (provider == PostgreSql)
        {
            Assert.True(failure.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.CheckViolation, ConstraintName: SessionStateConstraint }, UnexpectedConstraint);
        }
        else
        {
            Assert.Equal(SqlServer, provider);
            Assert.True(failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerCheckViolation }, UnexpectedConstraint);
        }
    }
}
