using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Reuses real-provider enrollment and native mechanisms for action-specific security assertions.</summary>
internal static class SecurityScenario
{
    private const string NoUnusedCode = "No unused native-window value was available for this bounded scenario.";
    internal const string Login = "Login";
    internal const string Recovery = "Recovery";
    internal const string Replacement = "Replacement";
    internal const string Regeneration = "Regeneration";

    /// <summary>Admits one actual fresh-password action and requires the production result to remain restricted.</summary>
    internal static async Task<string> BeginAsync(JourneyScenario scenario, string purpose, JourneyCompletion.Grant? grant = null)
    {
        var result = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, purpose,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken, Candidate(grant));
        Assert.NotNull(result.Interaction);
        return result.Interaction;
    }

    /// <summary>Supplies only candidate cookie identifiers; the production operation must reread their current authority.</summary>
    internal static JourneySessionCandidate? Candidate(JourneyCompletion.Grant? grant) =>
        grant is null ? null : new JourneySessionCandidate(grant.UserId, grant.SessionId);

    /// <summary>Selects an independent native-window value absent from every retained accepted value for the current factor.</summary>
    internal static async Task<string> UnusedAsync(JourneyScenario scenario)
    {
        var token = TestContext.Current.CancellationToken;
        var generation = (await scenario.Database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token)).FactorGeneration;
        var rows = await scenario.Database.Set<AdministratorReplay>().AsNoTracking().Where(row => row.Generation == generation).ToArrayAsync(token);
        var accepted = rows.Select(row => scenario.Get<FactorProtection>().Unprotect(row.ProtectedCode, row.UserId, row.Generation,
            FactorProtection.Replay)).ToHashSet(StringComparer.Ordinal);
        const int StepSeconds = 30;
        const int NativeWindowSteps = 2;
        int[] offsets = [0, StepSeconds, -StepSeconds, StepSeconds * NativeWindowSteps, -StepSeconds * NativeWindowSteps];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = offsets.Select(offset => FactorOracle.Code(scenario.Seed, now + offset)).FirstOrDefault(value => !accepted.Contains(value));
        Assert.True(code is not null, NoUnusedCode);
        return code!;
    }

    /// <summary>Reads fresh detached state after mutations so assertions cannot be satisfied by stale tracked entities.</summary>
    internal static Task<AdministratorInteraction> InteractionAsync(JourneyScenario scenario, string publicId) =>
        scenario.Database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(row => row.PublicId == publicId, TestContext.Current.CancellationToken);
}
