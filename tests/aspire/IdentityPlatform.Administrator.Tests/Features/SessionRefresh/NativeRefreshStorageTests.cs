using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Characterizes installed native status/manager semantics, not an enabled product refresh grant or HTTP authentication flow.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRefreshStorageTests(BootstrapFixture fixture)
{
    /// <summary>Strict native reuse rejection invalidates linked tokens but leaves authorization valid; an unrelated family is untouched.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RedeemedRefresh_When_NullLeewayValidationCommits_Then_LinkedTokensButNotAuthorizationAreRevoked(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario, redeemed: true);
        var unrelated = await NativeRefreshRows.CreateAsync(scenario);
        var rejected = await scenario.CommitAsync(async (scope, _) =>
        {
            var result = await NativeRefreshStages.ValidateAsync(scope, family.Predecessor);
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, result.Error);
            return result.IsRejected;
        });
        Assert.True(rejected);
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Revoked),
            await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, unrelated));
    }

    /// <summary>Zero is not disabled leeway when the native redeemed entry lacks a date; null rejects that same shape.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RedeemedEntryWithoutDate_When_ZeroAndNullLeewayAreCompared_Then_OnlyNullAlwaysRejects(string provider)
    {
        foreach (var leeway in new TimeSpan?[] { TimeSpan.Zero, null })
        {
            await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider, leeway);
            var family = await NativeRefreshRows.CreateAsync(scenario, redeemed: true, redemptionDate: false);
            var rejected = await scenario.CommitAsync(async (scope, _) =>
                (await NativeRefreshStages.ValidateAsync(scope, family.Predecessor)).IsRejected);
            Assert.Equal(leeway is null, rejected);
            var state = await NativeRefreshRows.StatusAsync(scenario, family);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, state.Authorization);
            Assert.Equal(leeway is null ? OpenIddictConstants.Statuses.Revoked : OpenIddictConstants.Statuses.Valid, state.Successor);
        }
    }

    /// <summary>Native replay effects are transactional and cannot be reported durable merely because a context rejects.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ReplayRevocation_When_OwnerRollsBack_Then_CommittedFamilyRemainsUnchanged(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario, redeemed: true);
        var before = await NativeRefreshRows.StatusAsync(scenario, family);
        await scenario.RunAsync(async (scope, _) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            Assert.True((await NativeRefreshStages.ValidateAsync(scope, family.Predecessor)).IsRejected);
            return true;
        });
        Assert.Equal(before, await NativeRefreshRows.StatusAsync(scenario, family));
    }

    /// <summary>Manager-owned authorization and token denial rollback together, then commit together and deny a successor through native validation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_FamilyRevocation_When_RollbackThenCommitAreObserved_Then_OnlyCommittedDenialRejectsSuccessor(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        var before = await NativeRefreshRows.StatusAsync(scenario, family);
        await scenario.RunAsync(async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            return await NativeRefreshRows.RevokeFamilyAsync(scope, family, token);
        });
        Assert.Equal(before, await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.False(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Successor)).IsRejected));
        await scenario.CommitAsync((scope, token) => NativeRefreshRows.RevokeFamilyAsync(scope, family, token));
        Assert.Equal((OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Revoked),
            await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.True(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Successor)).IsRejected));
    }

    /// <summary>Native authorization validation must reject a still-valid token after only its authorization is revoked.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ValidSuccessor_When_OnlyItsAuthorizationIsRevoked_Then_NativeAuthorizationValidationDeniesThatFamily(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        var unrelated = await NativeRefreshRows.CreateAsync(scenario);
        Assert.False(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Successor)).IsRejected));
        await scenario.CommitAsync(async (scope, token) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorization = await manager.FindByIdAsync(family.Authorization, token);
            Assert.NotNull(authorization);
            return await manager.TryRevokeAsync(authorization, token);
        });
        Assert.Equal((OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.True(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Successor)).IsRejected));
        Assert.False(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, unrelated.Successor)).IsRejected));
    }

    /// <summary>A committed early consumption remains redeemed after a later independent issuance epoch rolls back.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_DurableEarlyRedemption_When_LaterEpochRollsBack_Then_PredecessorDoesNotBecomeValidAgain(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.CommitAsync(async (scope, _) =>
        {
            var context = await NativeRefreshStages.SignInAsync(scope, family.Predecessor);
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRejected);
            return true;
        });
        await scenario.RunAsync(async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            return await NativeRefreshRows.RevokeFamilyAsync(scope, family, token);
        });
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Redeemed, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.True(await scenario.CommitAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Predecessor)).IsRejected));
    }

    /// <summary>Both providers must discover and execute the identical native semantic controls.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}

/// <summary>Centralizes only fixture identities; expected native decisions remain independent in each test.</summary>
internal static class NativeRefreshProviders
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Names = ["PostgreSql", "SqlServer"];

    internal static IEnumerable<TheoryDataRow<string>> Rows => Names
        .Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
}
