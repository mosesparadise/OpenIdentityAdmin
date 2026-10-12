using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Requires recovery password admission to remain restricted and preserve all existing credential authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryAdmissionTests(BootstrapFixture fixture)
{
    /// <summary>A native password admits a code challenge without staging a replacement, spending codes or granting another session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_EnrolledAccount_When_RecoveryPasswordIsVerified_Then_OnlyRestrictedCodeChallengeIsAdmitted(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        const string Recovery = "Recovery";
        var result = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password,
            Recovery, scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Interaction);
        var interaction = await scenario.Database.Set<AdministratorInteraction>().AsNoTracking()
            .SingleAsync(row => row.PublicId == result.Interaction, TestContext.Current.CancellationToken);
        Assert.Equal(Recovery, interaction.Purpose);
        Assert.True(interaction.StagedSeed is null);
        Assert.Equal(1, await scenario.Database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(10, await scenario.Database.Set<AdministratorRecoveryCode>()
            .CountAsync(row => row.RedeemedAt == null, TestContext.Current.CancellationToken));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }
}
