using System.Security.Claims;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Checks current persisted grants using real isolated provider scopes and independent finite expected sets.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantTests(BootstrapFixture fixture)
{
    private const string PrincipalAuthentication = "fixture";
    /// <summary>Each eligible account receives its actual bundle, while specialist state changes remove current authority.</summary>
    [Theory, MemberData(nameof(GrantCases.Bundles), MemberType = typeof(GrantCases))]
    public async Task Given_RecognizedBundle_When_CurrentGrantsAreRead_Then_OnlyItsPersistedFiniteSetIsReturned(string provider, string bundle)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await GrantState.SetMembershipsAsync(scenario, bundle);
        var observation = await scenario.ReadAsync();
        GrantState.RequirePermissions(GrantCases.Expected(bundle), observation);
        if (bundle == GrantCases.Platform)
        {
            Assert.Equal(scenario.ActorId, Assert.Single(observation.UsableAdministrators));
        }
        else
        {
            Assert.Empty(observation.UsableAdministrators);
            await GrantState.RequireSpecialistStateAsync(scenario, bundle);
        }
    }

    /// <summary>Overlapping recognized roles contribute each stored permission once and ignore unrelated claim types.</summary>
    [Theory, MemberData(nameof(GrantCases.Overlap), MemberType = typeof(GrantCases))]
    public async Task Given_OverlappingRecognizedRoles_When_CurrentGrantsAreRead_Then_DistinctPersistedGrantsAreUnioned(string provider, string variant)
    {
        Assert.Equal(GrantCases.OverlapCase, variant);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await GrantState.SetMembershipsAsync(scenario, GrantCases.Application, GrantCases.Auditor);
        await GrantState.AddClaimAsync(scenario, GrantCases.Application, GrantCases.UnrelatedType, GrantCases.UnrelatedValue);
        var expected = GrantCases.Expected(GrantCases.Application).Union(GrantCases.Expected(GrantCases.Auditor), StringComparer.Ordinal);
        GrantState.RequirePermissions(expected, await scenario.ReadAsync());
    }

    /// <summary>User claims, ambient principal claims and ordinary custom membership cannot replace recognized role grants.</summary>
    [Theory, MemberData(nameof(GrantCases.NonAuthority), MemberType = typeof(GrantCases))]
    public async Task Given_NonauthoritativeClaims_When_CurrentGrantsAreRead_Then_NoAuthorityIsInvented(string provider, string variant)
    {
        var accessor = new HttpContextAccessor();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.AddSingleton<IHttpContextAccessor>(accessor));
        await GrantState.SetMembershipsAsync(scenario);
        if (variant == GrantCases.PrincipalClaim)
        {
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(GrantCases.ClaimType, GrantCases.UserRead)], PrincipalAuthentication))
            };
        }
        try
        {
            await GrantState.AddNonAuthorityAsync(scenario, accessor, variant);
            var observation = await scenario.ReadAsync();
            Assert.Empty(observation.Permissions);
            Assert.Empty(observation.UsableAdministrators);
        }
        finally { accessor.HttpContext = null; }
    }

    /// <summary>Malformed native authority denies evaluation, or is rejected by an independently identified provider constraint.</summary>
    [Theory, MemberData(nameof(GrantCases.Malformed), MemberType = typeof(GrantCases))]
    public async Task Given_MalformedReservedAuthority_When_CurrentGrantsAreRead_Then_EvaluationDenies(string provider, string variant)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var initial = await scenario.SnapshotAsync();
        if (!await MalformedGrants.PersistAsync(scenario, provider, variant))
        {
            Assert.Equal(initial, await scenario.SnapshotAsync());
            return; // Exact native FK evidence; never credited as a successfully persisted policy case.
        }
        var before = await scenario.SnapshotAsync();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ReadAsync());
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>The actual membership primary key prevents duplicate authority without fabricated duplicate SQL rows.</summary>
    [Theory, MemberData(nameof(GrantCases.Duplicate), MemberType = typeof(GrantCases))]
    public async Task Given_DuplicateMembershipIdentity_When_Persisted_Then_NoAmbiguousAuthorityCanExist(string provider, string variant)
    {
        Assert.Equal(GrantCases.DuplicateMembership, variant);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var before = await scenario.SnapshotAsync();
        await GrantState.RequireDuplicateConstraintAsync(scenario, provider);
        Assert.Equal(before, await scenario.SnapshotAsync());
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Platform), await scenario.ReadAsync());
    }

    /// <summary>A committed deletion in an independent context invalidates later observations and remains absent for specialists.</summary>
    [Theory, MemberData(nameof(GrantCases.Removal), MemberType = typeof(GrantCases))]
    public async Task Given_CurrentPersistedGrants_When_AnotherStoreRemovesOne_Then_NextEvaluationReflectsRemoval(string provider, string variant)
    {
        Assert.Equal(GrantCases.CrossStoreRemoval, variant);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await GrantState.SetMembershipsAsync(scenario, GrantCases.Identity);
        await using var reader = scenario.CreateScope();
        var service = reader.ServiceProvider.GetRequiredService<GovernanceService>();
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Identity), await service.ReadAsync(scenario.ActorId, TestContext.Current.CancellationToken));
        await GrantState.RemoveClaimAsync(scenario, GrantCases.Identity, GrantCases.UserRead);
        var expected = GrantCases.Expected(GrantCases.Identity).Where(value => value != GrantCases.UserRead);
        GrantState.RequirePermissions(expected, await service.ReadAsync(scenario.ActorId, TestContext.Current.CancellationToken));
        GrantState.RequirePermissions(expected, await scenario.ReadAsync());
    }

    /// <summary>The finite immutable request boundary rejects an unknown permission despite the actor's valid native grant set.</summary>
    [Theory, MemberData(nameof(GrantCases.UnknownRequest), MemberType = typeof(GrantCases))]
    public async Task Given_UnknownRequestedPermission_When_Evaluated_Then_DecisionDenies(string provider, string variant)
    {
        Assert.Equal(GrantCases.UnknownRequestCase, variant);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Platform), await scenario.ReadAsync());
        var roleId = await GrantState.RoleIdAsync(scenario, GrantCases.Identity);
        var valid = await scenario.RequestAsync(GovernanceAction.AddGrant, roleId: roleId, permission: GrantCases.UserRead);
        var before = await scenario.SnapshotAsync();
        Assert.Null(GovernanceRequest.Create(GovernanceAction.AddGrant, valid.Versions, roleId: roleId, permission: GrantCases.Unknown));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
