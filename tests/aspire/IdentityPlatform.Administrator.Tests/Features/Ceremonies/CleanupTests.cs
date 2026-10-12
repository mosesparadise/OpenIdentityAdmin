using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Proves bounded metadata reclamation cannot erase live evidence, audit or permanent bootstrap state.</summary>
[Collection("administrator-bootstrap")]
public sealed class CleanupTests(BootstrapFixture fixture)
{
    private const int OverBatch = 101;
    private const int Lifetime = 300;
    private const string Consumed = "Consumed";
    private const string ReplayMaterial = "not-decrypted-by-cleanup";

    /// <summary>One pass removes at most one hundred rows per category while preserving live authority and permanent audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TerminalBacklog_When_CleanupRuns_Then_CategoryBoundsAndLiveEvidenceArePreserved(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var now = clock.Now.ToUnixTimeSeconds();
        SeedBacklog(factors, scenario, now);
        await factors.Database.SaveChangesAsync(factors.Token);
        Assert.Equal(400, await scenario.Get<CeremonyCleanup>().RunAsync(factors.Token));
        Assert.Equal(2, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        Assert.Equal(2, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorSession>().CountAsync(factors.Token));
        Assert.Equal(2, await factors.Database.Set<AdministratorReplay>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorBootstrapReceipt>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<SecurityAuditIntent>().CountAsync(factors.Token));
        Assert.Equal(now, (await factors.Database.Set<AdministratorAuthentication>().SingleAsync(factors.Token)).ObservedAt);
        Assert.Equal(4, await scenario.Get<CeremonyCleanup>().RunAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
    }

    /// <summary>Creates terminal rows and one live replay entry using independent retention values.</summary>
    private static void SeedBacklog(FactorScenario factors, CeremonyScenario scenario, long now)
    {
        var template = scenario.Interaction;
        for (var index = 0; index < OverBatch; index++)
        {
            factors.Database.Add(new AdministratorBrowserBudget { Id = CeremonySecrets.Create(), ExpiresAt = 1 });
            factors.Database.Add(new AdministratorInteraction
            {
                UserId = scenario.UserId,
                PublicId = CeremonySecrets.Create(),
                BrowserBudgetId = template.BrowserBudgetId,
                BrowserDigest = template.BrowserDigest,
                Purpose = template.Purpose,
                SecurityStamp = template.SecurityStamp,
                State = Consumed,
                CreatedAt = 1,
                ExpiresAt = Lifetime
            });
            factors.Database.Add(AdmissionTests.Session(scenario, now, revoked: true));
            factors.Database.Add(new AdministratorReplay
            {
                UserId = scenario.UserId,
                ProtectedCode = ReplayMaterial,
                AcceptedAt = now - Lifetime - 1,
                ExpiresAt = now - 1
            });
        }
        factors.Database.Add(new AdministratorReplay { UserId = scenario.UserId, ProtectedCode = ReplayMaterial, AcceptedAt = now, ExpiresAt = now + Lifetime });
    }
}
