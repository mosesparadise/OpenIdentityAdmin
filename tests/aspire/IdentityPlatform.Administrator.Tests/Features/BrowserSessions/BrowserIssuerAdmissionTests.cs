using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves explicit password retry can reconcile only the exact pristine durable admission, never changed authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerAdmissionTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int LifetimeSeconds = 300;

    /// <summary>Retains actual provider traits for every independent pristine-admission corruption boundary.</summary>
    public static IEnumerable<TheoryDataRow<string, BrowserAdmissionFault>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Enum.GetValues<BrowserAdmissionFault>()
        select new TheoryDataRow<string, BrowserAdmissionFault>(provider, fault).WithTrait(ProviderTrait, provider);

    /// <summary>Changed original state or a different protected browser cannot reconcile an admission and leaves the persisted snapshot unchanged.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ChangedPendingAdmission_When_ExplicitPasswordRetryReconciles_Then_NoAuthorityOrAuditIsChanged(string provider, BrowserAdmissionFault fault)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var attempt = IssuerContinuationProviderFixture.Create();
        var expires = scenario.Clock.GetUtcNow().ToUnixTimeSeconds() + LifetimeSeconds;
        Assert.True(await AdmitAsync(scenario, attempt.Request, expires));
        if (fault != BrowserAdmissionFault.Browser)
        {
            await using var scope = scenario.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<IssuerContinuation>().SingleAsync(TestContext.Current.CancellationToken);
            if (fault == BrowserAdmissionFault.Revision)
            {
                row.Revision++;
            }
            if (fault == BrowserAdmissionFault.Correlation)
            {
                row.CorrelationId = Guid.Empty;
            }
            if (fault == BrowserAdmissionFault.Observed)
            {
                row.ObservedAt = row.CreatedAt - 1;
            }
            if (fault == BrowserAdmissionFault.Retention)
            {
                row.RetainUntil++;
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var request = fault == BrowserAdmissionFault.Browser
            ? IssuerContinuationRequest.Read(attempt.Intent, IssuerContinuationProviderFixture.Selector())! : attempt.Request;
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        Assert.False(await AdmitAsync(scenario, request, expires));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Uses independent actual provider contexts for initial admission and explicit retry.</summary>
    private static async Task<bool> AdmitAsync(JourneyScenario scenario, IssuerContinuationRequest request, long expires)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IssuerContinuationService>()
            .AdmitForBrowserAsync(request, expires, TestContext.Current.CancellationToken);
    }
}

/// <summary>Names distinct browser proof and pristine persisted-admission invariants.</summary>
public enum BrowserAdmissionFault
{
    Browser, Revision, Correlation, Observed, Retention
}
