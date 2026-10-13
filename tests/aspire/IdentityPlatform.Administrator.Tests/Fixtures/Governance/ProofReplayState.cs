using System.Globalization;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Provisions independently bounded replay occupancy with valid protected synthetic material; it never creates an authorization proof.</summary>
internal static class ProofReplayState
{
    private const int ReplayCapacity = 64;
    private const int CandidateMultiplier = 2;
    private const int RetentionSeconds = 300;
    private const int OlderThanRetentionSeconds = RetentionSeconds + 1;
    private const string CodeFormat = "D6";
    private const string ExpiredCode = "123456";

    /// <summary>Adds one genuinely prunable row with real protection while retaining the actual enrollment reservation.</summary>
    internal static async Task ExpiredAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var generation = await GenerationAsync(database, scenario.ActorId);
        var now = scenario.Journey.Clock.Now.ToUnixTimeSeconds();
        database.Add(new AdministratorReplay
        {
            UserId = scenario.ActorId,
            Generation = generation,
            AcceptedAt = now - OlderThanRetentionSeconds,
            ExpiresAt = now - 1,
            ProtectedCode = scope.ServiceProvider.GetRequiredService<FactorProtection>()
                .Protect(ExpiredCode, scenario.ActorId, generation, FactorProtection.Replay)
        });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId &&
            row.AcceptedAt < now - RetentionSeconds && row.ExpiresAt < now, TestContext.Current.CancellationToken));
    }

    /// <summary>Fills exactly sixty-four unprunable entries, preserving original rows and independently excluding a valid unused submitted code.</summary>
    internal static async Task<string> FillAsync(GovernanceScenario scenario)
    {
        var submitted = await scenario.UnusedCodeAsync();
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var generation = await GenerationAsync(database, scenario.ActorId);
        var rows = await database.Set<AdministratorReplay>().Where(row => row.UserId == scenario.ActorId && row.Generation == generation).ToListAsync(token);
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        var occupied = rows.Select(row => protection.Unprotect(row.ProtectedCode, scenario.ActorId, generation, FactorProtection.Replay))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(submitted, occupied);
        var now = scenario.Journey.Clock.Now.ToUnixTimeSeconds();
        Assert.InRange(rows.Count, 1, ReplayCapacity - 1);
        Assert.All(rows, row => Assert.True(row.AcceptedAt >= now - RetentionSeconds && row.ExpiresAt > now));
        occupied.Add(submitted);
        foreach (var value in Enumerable.Range(0, ReplayCapacity * CandidateMultiplier).Select(value => value.ToString(CodeFormat, CultureInfo.InvariantCulture)))
        {
            if (rows.Count == ReplayCapacity)
            {
                break;
            }
            if (!occupied.Add(value))
            {
                continue;
            }
            rows.Add(Create(protection, scenario.ActorId, generation, value, now));
            database.Add(rows[^1]);
        }
        await database.SaveChangesAsync(token);
        Assert.Equal(ReplayCapacity, await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId && row.Generation == generation, token));
        return submitted;
    }

    /// <summary>Protects only synthetic six-digit occupancy under the same actual user and active factor generation.</summary>
    private static AdministratorReplay Create(FactorProtection protection, string actor, long generation, string code, long now) => new()
    {
        UserId = actor,
        Generation = generation,
        AcceptedAt = now,
        ExpiresAt = now + RetentionSeconds,
        ProtectedCode = protection.Protect(code, actor, generation, FactorProtection.Replay)
    };

    /// <summary>Reads the actual current enrollment generation rather than assuming the initial allocator value.</summary>
    private static Task<long> GenerationAsync(PlatformDbContext database, string actor) => database.Set<AdministratorEnrollment>()
        .Where(row => row.UserId == actor).Select(row => row.FactorGeneration).SingleAsync(TestContext.Current.CancellationToken);
}
