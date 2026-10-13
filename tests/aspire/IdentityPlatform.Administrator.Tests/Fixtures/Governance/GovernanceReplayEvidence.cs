using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Retains only nonsecret authority, session and replay witnesses around the second native factor attempt.</summary>
internal sealed record GovernanceReplayEvidence(long Revision, bool Membership, string Replay, string Sessions,
    int Failures, int DeniedAudits, int AcceptedAudits)
{
    private const string Denied = "Denied";
    private const string Accepted = "Accepted";
    private const string CeremonyDenied = "denied";
    private const string CeremonyAccepted = "accepted";

    /// <summary>Reads detached committed rows after each independent operation has completely released its transaction.</summary>
    internal static async Task<GovernanceReplayEvidence> ReadAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return new(
            await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token),
            await database.UserRoles.AnyAsync(row => row.UserId == request.UserId && row.RoleId == request.RoleId, token),
            Hash(await database.Set<AdministratorReplay>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)),
            Hash(await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)),
            await database.Set<AdministratorAuthentication>().Where(row => row.UserId == scenario.ActorId).Select(row => row.Failures).SingleAsync(token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Outcome == Denied || row.Outcome == CeremonyDenied, token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Outcome == Accepted || row.Outcome == CeremonyAccepted, token));
    }

    /// <summary>Requires exactly one ordinary failed-factor budget/audit effect and no second authority, session or proof reservation.</summary>
    internal void RequireOnlyDenial(GovernanceReplayEvidence after)
    {
        Assert.Equal(Revision, after.Revision);
        Assert.Equal(Membership, after.Membership);
        Assert.Equal(Replay, after.Replay);
        Assert.Equal(Sessions, after.Sessions);
        Assert.Equal(Failures + 1, after.Failures);
        Assert.Equal(DeniedAudits + 1, after.DeniedAudits);
        Assert.Equal(AcceptedAudits, after.AcceptedAudits);
    }

    /// <summary>Prevents credential or session material from appearing in assertion diagnostics.</summary>
    private static string Hash<T>(T rows) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
}
