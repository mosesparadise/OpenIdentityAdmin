using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Separates exact expected revocation/reactivation fields from secret-free digests of every unchanged target credential and authority row.</summary>
internal sealed record ReactivationState(string Stable, string Rows, string RevokedRows, bool Enabled, bool Suspended,
    long Generation, long AuthenticationRevision, long EnrollmentRevision, long AuthorityRevision, string NativeStamp,
    int Failures, int LiveSessions, int LiveLocalSessions, int PendingInteractions)
{
    private const string Pending = "Pending";
    private const string Consumed = "Consumed";

    /// <summary>Captures the actual target state after writers join; protected material is compared only through digests.</summary>
    internal static async Task<ReactivationState> ReadAsync(GovernanceHttpBridge bridge)
    {
        await using var scope = bridge.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actor = bridge.Original.UserId;
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.AsNoTracking().SingleAsync(row => row.Id == actor, token);
        Assert.False(string.IsNullOrEmpty(user.ConcurrencyStamp));
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == actor, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(row => row.UserId == actor, token);
        var sessions = await database.Set<AdministratorSession>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token);
        var local = await database.Set<LocalIdentitySession>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token);
        var interactions = await database.Set<AdministratorInteraction>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token);
        var revision = await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token);
        var result = new ReactivationState(string.Empty, Hash(new
        {
            sessions,
            local,
            interactions
        }), string.Empty,
            user.IsEnabled, enrollment.Suspended, authentication.Generation, authentication.Revision, enrollment.Revision, revision,
            Hash(user.ConcurrencyStamp), authentication.Failures, sessions.Count(row => !row.Revoked), local.Count(row => !row.Revoked),
            interactions.Count(row => row.State == Pending));
        Normalize(user, enrollment, authentication);
        return result with
        {
            RevokedRows = Revoke(sessions, local, interactions),
            Stable = Hash(new
            {
                user,
                enrollment,
                authentication,
                Material = await MaterialAsync(database, actor)
            })
        };
    }

    /// <summary>Checks exact generation, coordination, flag, native version and prior-row effects for one governed account transition.</summary>
    internal static void Require(ReactivationState before, ReactivationState after, bool disable, bool restoring)
    {
        Assert.Equal(before.Stable, after.Stable);
        Assert.Equal(before.AuthorityRevision + 1, after.AuthorityRevision);
        Assert.Equal(before.AuthenticationRevision + 1, after.AuthenticationRevision);
        Assert.Equal(before.EnrollmentRevision + 1, after.EnrollmentRevision);
        Assert.Equal(before.Generation + (restoring ? 0 : 1), after.Generation);
        Assert.Equal(disable ? restoring : true, after.Enabled);
        Assert.Equal(!disable && !restoring, after.Suspended);
        Assert.Equal(before.Failures, after.Failures);
        Assert.Equal(restoring ? before.Rows : before.RevokedRows, after.Rows);
        if (disable)
        {
            Assert.NotEqual(before.NativeStamp, after.NativeStamp);
        }
        else
        {
            Assert.Equal(before.NativeStamp, after.NativeStamp);
        }
        Assert.Equal(0, after.LiveSessions);
        Assert.Equal(0, after.LiveLocalSessions);
        Assert.Equal(0, after.PendingInteractions);
    }

    /// <summary>Normalizes only the fields separately checked by the exact transition oracle; credentials and factor/enrollment state stay in the digest.</summary>
    private static void Normalize(PlatformUser user, AdministratorEnrollment enrollment, AdministratorAuthentication authentication)
    {
        user.IsEnabled = true;
        user.ConcurrencyStamp = null;
        enrollment.Suspended = false;
        enrollment.Revision = 0;
        authentication.Generation = 0;
        authentication.Revision = 0;
    }

    /// <summary>Computes the independent expected revocation over detached copies without writing or accepting replacement persisted snapshots.</summary>
    private static string Revoke(AdministratorSession[] sessions, LocalIdentitySession[] local, AdministratorInteraction[] interactions)
    {
        foreach (var session in sessions.Where(row => !row.Revoked))
        {
            session.Revoked = true;
            session.Revision++;
        }
        foreach (var session in local.Where(row => !row.Revoked))
        {
            session.Revoked = true;
        }
        foreach (var interaction in interactions.Where(row => row.State == Pending))
        {
            interaction.State = Consumed;
            interaction.StagedSeed = null;
            interaction.Revision++;
        }
        return Hash(new
        {
            sessions,
            local,
            interactions
        });
    }

    /// <summary>Preserves active factor/recovery/replay/native auxiliary and target-role material across both disabling and reactivation.</summary>
    private static async Task<string> MaterialAsync(PlatformDbContext database, string actor)
    {
        var token = TestContext.Current.CancellationToken;
        return Hash(new
        {
            Factors = await database.Set<AdministratorFactor>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token),
            Recovery = await database.Set<AdministratorRecoveryCode>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token),
            Replay = await database.Set<AdministratorReplay>().AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token),
            Memberships = await database.UserRoles.AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.RoleId).ToArrayAsync(token),
            Claims = await database.UserClaims.AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.Id).ToArrayAsync(token),
            Tokens = await database.UserTokens.AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.LoginProvider).ThenBy(row => row.Name).ToArrayAsync(token),
            Logins = await database.UserLogins.AsNoTracking().Where(row => row.UserId == actor).OrderBy(row => row.LoginProvider).ThenBy(row => row.ProviderKey).ToArrayAsync(token)
        });
    }

    /// <summary>Returns only safe comparison digests, never printable plaintext session, factor or password fields.</summary>
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
