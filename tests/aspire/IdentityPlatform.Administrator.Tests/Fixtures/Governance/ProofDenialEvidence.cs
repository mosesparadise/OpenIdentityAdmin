using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Separates exactly permitted bad-credential effects from unchanged authority, material and earlier audits.</summary>
internal sealed record ProofDenialEvidence(string Stable, string Audits, Guid[] AuditIds, long Semantic,
    long GovernanceRevision, long AuthenticationRevision, int Failures, long? ObservedAt, long? LockoutUntil, ProofNativeBudget Native)
{
    private const int EarlierFailures = 2;
    private const int BeforeLockoutFailures = 4;
    private const int LockoutFailures = 5;
    private const int LockoutSeconds = 300;
    private const string WrongPassword = "incorrect-synthetic-password";
    private const string Event = "AdministratorGrantAdded";
    private const string Denied = "Denied";
    private const string UserType = "User";
    private const string RoleType = "Role";

    /// <summary>Seeds a nonzero existing budget and requires one real denial without target writes, password-success reset or replay consumption.</summary>
    internal static Task RequireAsync(GovernanceScenario scenario, GovernanceRequest request, string condition) =>
        RequireDenialAsync(scenario, request, condition, EarlierFailures, false);

    /// <summary>Requires the fifth native failure to commit lockout through the same denial path instead of failing its own final session check.</summary>
    internal static async Task RequireFifthAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        await RequireDenialAsync(scenario, request, ProofCases.Totp, BeforeLockoutFailures, true);
        await ProofNativeBudget.RequireSessionDeniedAsync(scenario);
    }

    /// <summary>Runs the real entrypoint and permits only independently specified budget, lock coordination and audit effects.</summary>
    private static async Task RequireDenialAsync(GovernanceScenario scenario, GovernanceRequest request, string condition,
        int earlierFailures, bool locksAccount)
    {
        await SeedFailuresAsync(scenario, earlierFailures);
        var before = await ReadAsync(scenario, locksAccount);
        Assert.Null(before.LockoutUntil);
        Assert.Null(before.Native.LockoutUntil);
        var code = condition == ProofCases.Totp ? FactorOracle.InvalidCode(scenario.Journey.Seed) : await scenario.UnusedCodeAsync();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.Equal(GovernanceOutcome.Denied, await scenario.ExecuteAsync(request,
            password: condition == ProofCases.Password ? WrongPassword : scenario.Password, code: code));
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, condition != ProofCases.Totp);
        var after = await ReadAsync(scenario, locksAccount);
        Assert.Equal(before.Stable, after.Stable);
        Assert.Equal(before.Semantic, after.Semantic);
        Assert.Equal(before.GovernanceRevision + 1, after.GovernanceRevision);
        Assert.Equal(before.AuthenticationRevision + 1, after.AuthenticationRevision);
        Assert.Equal(earlierFailures + 1, after.Failures);
        Assert.NotNull(before.ObservedAt);
        Assert.NotNull(after.ObservedAt);
        Assert.Equal(Math.Max(before.ObservedAt.Value, scenario.Journey.Clock.Now.ToUnixTimeSeconds()), after.ObservedAt.Value);
        RequireLockout(scenario, before, after, locksAccount);
        await RequireAuditAsync(scenario, request, before);
    }

    /// <summary>Sets only coherent product failure occupancy before the command; no native proof or session state is manufactured.</summary>
    internal static async Task SeedFailuresAsync(GovernanceScenario scenario, int failures)
    {
        await using var setup = scenario.CreateScope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<AdministratorAuthentication>().Where(row => row.UserId == scenario.ActorId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Failures, failures), TestContext.Current.CancellationToken));
    }

    /// <summary>Requires exact product/native lockout effects and isolates the one native concurrency-stamp change at the fifth attempt.</summary>
    private static void RequireLockout(GovernanceScenario scenario, ProofDenialEvidence before, ProofDenialEvidence after, bool locked)
    {
        if (!locked)
        {
            Assert.Null(after.LockoutUntil);
            Assert.Equal(before.Native, after.Native);
            return;
        }
        Assert.Equal(LockoutFailures, after.Failures);
        var expected = scenario.Journey.Clock.Now.ToUnixTimeSeconds() + LockoutSeconds;
        Assert.Equal(expected, after.LockoutUntil);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(expected), after.Native.LockoutUntil);
        Assert.NotEqual(before.Native.ConcurrencyDigest, after.Native.ConcurrencyDigest);
    }

    /// <summary>Captures only hashes and allowed scalar deltas; native credentials and protected material never enter assertion output.</summary>
    private static async Task<ProofDenialEvidence> ReadAsync(GovernanceScenario scenario, bool permitsLockout)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(token);
        var governance = await database.Set<AdministratorGovernance>().AsNoTracking().SingleAsync(token);
        var lockout = authentication.LockoutUntil;
        var users = await database.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token);
        var native = ProofNativeBudget.Capture(users.Single(row => row.Id == scenario.ActorId), permitsLockout);
        var revision = authentication.Revision;
        var failures = authentication.Failures;
        var observed = authentication.ObservedAt;
        authentication.Revision = 0;
        authentication.Failures = 0;
        authentication.ObservedAt = null;
        if (permitsLockout)
        {
            authentication.LockoutUntil = null;
        }
        var stable = new
        {
            Authentication = authentication,
            Users = users,
            Roles = await database.Roles.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Claims = await database.RoleClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Memberships = await database.UserRoles.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.RoleId).ToArrayAsync(token),
            Material = await MaterialAsync(database)
        };
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token);
        return new(Hash(stable), Hash(audits), audits.Select(row => row.Id).ToArray(), governance.AuthorityRevision,
            governance.Revision, revision, failures, observed, lockout, native);
    }

    /// <summary>Includes all auxiliary native state and every factor/session/proof/replay collection in the no-side-effects digest.</summary>
    private static async Task<string> MaterialAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        return Hash(new
        {
            Enrollments = await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Factors = await database.Set<AdministratorFactor>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Interactions = await database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Replay = await database.Set<AdministratorReplay>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Recovery = await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            LocalSessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            UserClaims = await database.UserClaims.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Tokens = await database.UserTokens.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.LoginProvider).ThenBy(row => row.Name).ToArrayAsync(token),
            Logins = await database.UserLogins.AsNoTracking().OrderBy(row => row.LoginProvider).ThenBy(row => row.ProviderKey).ToArrayAsync(token)
        });
    }

    /// <summary>Requires exactly one independently specified denial audit and byte-identical preexisting audit rows.</summary>
    private static async Task RequireAuditAsync(GovernanceScenario scenario, GovernanceRequest request, ProofDenialEvidence before)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(before.Audits, Hash(audits.Where(row => before.AuditIds.Contains(row.Id)).ToArray()));
        var added = Assert.Single(audits, row => !before.AuditIds.Contains(row.Id));
        Assert.Equal(Event, added.Event);
        Assert.Equal(Denied, added.Outcome);
        Assert.Equal(UserType, added.ActorType);
        Assert.Equal(RoleType, added.TargetType);
        Assert.Equal(scenario.ActorId, added.ActorId);
        Assert.Equal(request.RoleId, added.TargetId);
        Assert.NotEqual(Guid.Empty, added.CorrelationId);
        Assert.Equal(scenario.Journey.Clock.Now.ToUnixTimeSeconds(), added.OccurredAt);
    }

    /// <summary>Returns a nonsecret comparison digest rather than a printable snapshot of passwords, stamps or protected factors.</summary>
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
