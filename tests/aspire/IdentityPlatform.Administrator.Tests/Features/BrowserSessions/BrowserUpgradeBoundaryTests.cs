using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises native retained-capacity, monotonic-time and checked-revision boundaries without extending production budgets.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserUpgradeBoundaryTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string AuditTable = "SecurityAuditIntent";
    private const string Insert = "INSERT";
    private const int AllowedRollback = 30;
    private const int RejectedRollback = 31;
    private const int TokenLimit = 20000;
    private const int RequestLimit = 10000;
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly int[] Rollbacks = [AllowedRollback, RejectedRollback];
    private static readonly bool[] Boundaries = [false, true];

    /// <summary>Small backward samples preserve the watermark; excessive rollback rejects the complete transaction unchanged.</summary>
    [Theory, MemberData(nameof(ClockCases))]
    public async Task Given_PersistedWatermark_When_ClockMovesBackward_Then_OnlyTheAllowedBoundaryCommits(string provider, int seconds)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        var input = BrowserUpgradeFixture.Input(scenario);
        scenario.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(before.Observed - seconds);
        if (seconds == AllowedRollback)
        {
            Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
            Assert.Equal(before.Observed, (await BrowserUpgradeFixture.ReadAsync(scenario)).Observed);
            await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        }
        else
        {
            await BrowserUpgradeFixture.RejectAsync(scenario, input);
            Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
        }
    }

    /// <summary>Rollback after the actual audit insert reaches the final time guard and rolls back all staged effects.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_StagedUpgradeAudit_When_FinalClockRollsBack_Then_NoUpgradeEffectCommits(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider,
            configure: services => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(fence)));
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        fence.Arm(AuditTable, () => scenario.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(before.Observed - RejectedRollback), Insert);
        await BrowserUpgradeFixture.RejectAsync(scenario, BrowserUpgradeFixture.Input(scenario));
        Assert.True(fence.Triggered);
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
    }

    /// <summary>The last representable transition preserves its full revision in the bounded audit outcome and reconciles without overflow.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_LastUpgradableRevision_When_UpgradeAndReplayRun_Then_MaximumRevisionIsRecordedExactly(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await NativeTokenBudgetFixture.WriteAsync(scenario.Services, async (scope, token) =>
        {
            await scope.Owner.RunAsync(async current =>
            {
                var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current);
                profile.Revision = long.MaxValue - 1;
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
            return (true, true);
        }, TestContext.Current.CancellationToken);
        var input = BrowserUpgradeFixture.Input(scenario) with
        {
            ExpectedRevision = long.MaxValue - 1
        };
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        var after = await BrowserUpgradeFixture.ReadAsync(scenario);
        Assert.Equal(long.MaxValue, after.Revision);
        await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        Assert.False(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        Assert.True(after == await BrowserUpgradeFixture.ReadAsync(scenario));
    }

    /// <summary>Over-capacity retained rows reject unchanged; removing exactly one fixture row permits the inclusive limit.</summary>
    [Theory, MemberData(nameof(CapacityCases))]
    public async Task Given_RetainedNativeCapacity_When_UpgradeRuns_Then_TheExactInclusiveLimitIsEnforced(string provider, bool requests)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var token = TestContext.Current.CancellationToken;
        var type = requests ? OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken : OpenIddictConstants.TokenTypeIdentifiers.AccessToken;
        await NativeTokenBudgetFixture.FillAsync(scenario.Services, (requests ? RequestLimit : TokenLimit) + 1, false, type, OpenIddictConstants.Statuses.Redeemed, token);
        var input = BrowserUpgradeFixture.Input(scenario);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        await BrowserUpgradeFixture.RejectAsync(scenario, input);
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(scenario));
        await NativeTokenBudgetFixture.WriteAsync(scenario.Services, async (scope, cancellation) =>
        {
            await scope.Owner.RunAsync(async current =>
            {
                var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().FirstAsync(current);
                scope.Owner.Database.Remove(row);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, cancellation, write: true);
            return (true, true);
        }, token);
        Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
    }

    public static IEnumerable<TheoryDataRow<string>> ProviderCases => Providers.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
    public static IEnumerable<TheoryDataRow<string, int>> ClockCases => from provider in Providers from seconds in Rollbacks select new TheoryDataRow<string, int>(provider, seconds).WithTrait(ProviderTrait, provider);
    public static IEnumerable<TheoryDataRow<string, bool>> CapacityCases => from provider in Providers from requests in Boundaries select new TheoryDataRow<string, bool>(provider, requests).WithTrait(ProviderTrait, provider);
}
