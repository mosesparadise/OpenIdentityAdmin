using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Rejects capabilities that could turn metadata maintenance into unaudited authentication or erase clock evidence.</summary>
[Collection("administrator-bootstrap")]
public sealed class OwnershipTests(BootstrapFixture fixture)
{
    /// <summary>A metadata transaction cannot load authentication capabilities despite holding the same account lock.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_MaintenanceTransaction_When_AccountCapabilityIsRequested_Then_UnauditedAuthenticationIsDenied(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(factors.Database, scenario.Get<NativeOperation>(),
            new(false, false, [], [scenario.UserId]), factors.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.AccountAsync(transaction));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitAsync(CeremonyEvent.Factor, true, scenario.UserId, 0));
        Assert.Equal(1, await factors.Database.Set<SecurityAuditIntent>().CountAsync(factors.Token));
    }

    /// <summary>A copied watermark cannot authorize deletion of replay rows while leaving the persisted clock unchanged.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DetachedOrForeignAuthenticationState_When_ReplayPruningIsRequested_Then_EvidenceIsRetained(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        factors.Database.Add(new AdministratorReplay { UserId = scenario.UserId, AcceptedAt = 0, ExpiresAt = 1, ProtectedCode = string.Empty });
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var otherScope = services.Services.CreateAsyncScope();
        var other = otherScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var foreign = await other.Set<AdministratorAuthentication>().SingleAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync();
        await Assert.ThrowsAsync<FactorUnavailableException>(() => CeremonyPruning.ReplayAsync(other, transaction,
            foreign, scenario.Get<CeremonyClock>(), 1));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => CeremonyAccount.LoadAsync(other, transaction, scenario.UserId));
        var copied = new AdministratorAuthentication { UserId = scenario.UserId };
        await Assert.ThrowsAsync<FactorUnavailableException>(() => CeremonyPruning.ReplayAsync(factors.Database, transaction,
            copied, scenario.Get<CeremonyClock>(), 1));
        Assert.Null(copied.ObservedAt);
        Assert.Equal(1, await factors.Database.Set<AdministratorReplay>().CountAsync(factors.Token));
    }
}
