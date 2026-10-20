using IdentityPlatform.Administrator.Tests.Features.Governance.Policy;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises current admission through real password/factor enrollment, native persisted grants and the shared issuance locks.</summary>
[Collection("administrator-bootstrap")]
public sealed class AdmissionTests(BootstrapFixture fixture)
{
    private const int FreshnessSeconds = 300;
    private const int ShortAge = 30;
    private const int CodeSeconds = 60;
    private const int TokenSeconds = 300;
    private const int ExcessRollback = 31;
    private const int WiderAge = 600;

    /// <summary>Platform, specialist and Auditor grants qualify using original full source proof without importing permission claims into credentials.</summary>
    [Theory, MemberData(nameof(AdmissionCases.Bundles), MemberType = typeof(AdmissionCases))]
    public async Task Given_RealFullSession_When_CurrentBundleQualifies_Then_ClaimsRetainOriginalProofAndFiniteExpiry(string provider, string bundle)
    {
        await using var scenario = await AdmissionScenario.CreateAsync(fixture, provider);
        await GrantState.SetMembershipsAsync(scenario.Account, bundle);
        var original = await scenario.Account.Journey.Database.Set<AdministratorSession>().AsNoTracking()
            .SingleAsync(row => row.PublicId == scenario.Account.Session.SessionId, TestContext.Current.CancellationToken);
        var claims = await scenario.RunAsync(async (scope, result, token) =>
        {
            Assert.Equal(ProtocolAdmissionOutcome.Allowed, result.Outcome);
            Assert.NotNull(result.Proof);
            var clock = scope.Services.GetRequiredService<CeremonyClock>();
            Assert.True(await result.Proof.IsCurrentAsync(clock, token));
            return await result.Proof.DescribeAsync(clock, token);
        });
        Assert.NotNull(claims);
        Assert.Equal(original.UserId, claims.UserId);
        Assert.Equal(original.PublicId, claims.SessionId);
        Assert.Equal(original.PasswordVerifiedAt, claims.PasswordVerifiedAt);
        Assert.Equal(original.FactorVerifiedAt, claims.FactorVerifiedAt);
        Assert.Equal(original.ExpiresAt, claims.SourceExpiresAt);
        var now = scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds();
        Assert.Equal(now + CodeSeconds, claims.CodeExpiresAt);
        Assert.Equal(now + TokenSeconds, claims.TokenExpiresAt);
    }

    /// <summary>Equality, narrower requested age and clock rollback deny without renewing the password or factor observation.</summary>
    [Theory, MemberData(nameof(AdmissionCases.Time), MemberType = typeof(AdmissionCases))]
    public async Task Given_RealFullSession_When_ProofTimeReachesBoundary_Then_OnlyStrictlyFreshObservationQualifies(string provider, string boundary)
    {
        await using var scenario = await AdmissionScenario.CreateAsync(fixture, provider);
        var original = await scenario.Account.Journey.Database.Set<AdministratorSession>().AsNoTracking()
            .SingleAsync(row => row.PublicId == scenario.Account.Session.SessionId, TestContext.Current.CancellationToken);
        var (elapsed, maximum) = boundary switch
        {
            AdmissionCases.Before => (FreshnessSeconds - 1, (long?)null),
            AdmissionCases.Equality => (FreshnessSeconds, null),
            AdmissionCases.After => (FreshnessSeconds + 1, null),
            AdmissionCases.ShortBefore => (ShortAge - 1, (long?)ShortAge),
            AdmissionCases.ShortEquality => (ShortAge, ShortAge),
            AdmissionCases.Zero => (0, 0),
            AdmissionCases.Wider => (FreshnessSeconds, WiderAge),
            AdmissionCases.Rollback => (-ExcessRollback, null),
            _ => throw new ArgumentOutOfRangeException(nameof(boundary))
        };
        scenario.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.PasswordVerifiedAt + elapsed);
        var expected = boundary is AdmissionCases.Before or AdmissionCases.ShortBefore;
        await scenario.RunAsync(async (scope, result, token) =>
        {
            Assert.Equal(expected ? ProtocolAdmissionOutcome.Allowed : ProtocolAdmissionOutcome.LoginRequired, result.Outcome);
            if (expected)
            {
                var claims = await result.Proof!.DescribeAsync(scope.Services.GetRequiredService<CeremonyClock>(), token);
                Assert.NotNull(claims);
                Assert.Equal(original.PasswordVerifiedAt, claims.PasswordVerifiedAt);
                Assert.Equal(original.FactorVerifiedAt, claims.FactorVerifiedAt);
                Assert.Equal(scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds() + 1, claims.CodeExpiresAt);
            }
            else
            {
                Assert.Null(result.Proof);
            }
            return true;
        }, maximum);
    }

    /// <summary>Existing real recovery and persisted session damage cannot be promoted into fresh protocol authentication.</summary>
    [Theory, MemberData(nameof(AdmissionCases.Sessions), MemberType = typeof(AdmissionCases))]
    public async Task Given_RealSessionAuthority_When_SourceIsMissingChangedOrRestricted_Then_AdmissionRequiresLogin(string provider, string damage)
    {
        await using var scenario = await AdmissionScenario.CreateAsync(fixture, provider);
        var candidate = await GovernanceSessionDamage.ApplyAsync(scenario.Account, damage);
        await scenario.RunAsync((_, result, _) =>
        {
            Assert.Equal(ProtocolAdmissionOutcome.LoginRequired, result.Outcome);
            Assert.Null(result.Proof);
            return ValueTask.FromResult(true);
        }, session: candidate.SessionId);
    }
}
