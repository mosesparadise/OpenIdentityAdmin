using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Introduces one invalid session binding while retaining real native enrollment and explicit fixture ownership.</summary>
internal static class GovernanceSessionDamage
{
    internal const string Missing = "missing-session";
    internal const string Revoked = "revoked-session";
    internal const string DifferentUser = "different-user";
    internal const string AuthenticationGeneration = "changed-authentication-generation";
    internal const string FactorGeneration = "changed-factor-generation";
    internal const string SecurityStamp = "changed-security-stamp";
    internal const string Methods = "untruthful-methods";
    internal const string Idle = "idle-expired";
    internal const string Absolute = "absolute-expired";
    internal const string Recovery = "restricted-recovery";
    private const int IdleSeconds = 1800;
    private const int BeforeExpiry = 1;
    private const string RecoveryRequired = "RecoveryRequired";

    /// <summary>Returns untrusted candidate identifiers; every command must reread their actual stored authority.</summary>
    internal static async Task<JourneySessionCandidate> ApplyAsync(GovernanceScenario scenario, string variant)
    {
        if (variant == Missing)
        {
            var candidate = new JourneySessionCandidate(scenario.ActorId, CeremonySecrets.Create());
            await RequireNoSessionAsync(scenario, candidate.SessionId);
            return candidate;
        }
        if (variant == DifferentUser)
        {
            var other = await GovernanceAccounts.AddAsync(scenario);
            var session = await GovernanceAccounts.LoginAsync(scenario, other);
            Assert.NotEqual(scenario.ActorId, session.UserId);
            return new JourneySessionCandidate(scenario.ActorId, session.SessionId);
        }
        if (variant == Recovery)
        {
            return await RecoveryAsync(scenario);
        }
        await ChangeStoredAsync(scenario, variant);
        return scenario.Session;
    }

    /// <summary>Changes only the selected native binding, or advances the controlled product clock to one exact session boundary.</summary>
    private static async Task ChangeStoredAsync(GovernanceScenario scenario, string variant)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var session = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == scenario.Session.SessionId, token);
        switch (variant)
        {
            case Revoked:
                session.Revoked = true;
                break;
            case AuthenticationGeneration:
                var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == scenario.ActorId, token);
                authentication.Generation = checked(authentication.Generation + 1);
                break;
            case FactorGeneration:
                session.FactorGeneration = checked(session.FactorGeneration + 1);
                break;
            case SecurityStamp:
                var user = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
                user.SecurityStamp = Guid.NewGuid().ToString();
                break;
            case Idle:
                scenario.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(session.LastActivityAt + IdleSeconds);
                break;
            case Absolute:
                session.LastActivityAt = session.ExpiresAt - BeforeExpiry;
                scenario.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
        await database.SaveChangesAsync(token);
    }

    /// <summary>Redeems one real recovery code and presents the resulting restricted interaction as an untrusted session identifier.</summary>
    private static async Task<JourneySessionCandidate> RecoveryAsync(GovernanceScenario scenario)
    {
        var journey = scenario.Journey;
        var initial = await SecurityScenario.BeginAsync(journey, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var replacement = await journey.Get<JourneyTransition>().AuthorizeAsync(initial, journey.Correlation,
            SecurityScenario.Recovery, scenario.Grant.RecoveryCodes[0], correlation, TestContext.Current.CancellationToken);
        Assert.NotNull(replacement);
        var restricted = await SecurityScenario.InteractionAsync(journey, replacement);
        Assert.Equal(SecurityScenario.Replacement, restricted.Purpose);
        Assert.Null(restricted.SessionPublicId);
        Assert.Equal(RecoveryRequired, await journey.Database.Set<AdministratorEnrollment>().AsNoTracking()
            .Where(row => row.UserId == scenario.ActorId).Select(row => row.State).SingleAsync(TestContext.Current.CancellationToken));
        await RequireNoSessionAsync(scenario, replacement);
        return new JourneySessionCandidate(scenario.ActorId, replacement);
    }

    /// <summary>Verifies that candidate material does not accidentally identify a persisted full session.</summary>
    private static async Task RequireNoSessionAsync(GovernanceScenario scenario, string candidate)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<AdministratorSession>().AnyAsync(row => row.PublicId == candidate, TestContext.Current.CancellationToken));
    }
}
