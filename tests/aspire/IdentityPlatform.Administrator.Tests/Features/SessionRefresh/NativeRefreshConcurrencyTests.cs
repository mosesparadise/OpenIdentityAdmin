using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Proves native stale-update tolerance is not sufficient acceptance when the real owned store has failed.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRefreshConcurrencyTests(BootstrapFixture fixture)
{
    /// <summary>Two independently tracked rows deterministically conflict; native context acceptance cannot override poisoned commit ownership.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_StaleNativeRefreshRow_When_AnotherOwnerRedeems_Then_IgnoredNativeFailureStillCannotCommit(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        scenario.Concurrency.ExpectedToken = family.Predecessor.GetTokenId();
        var nativeReturned = false;
        var commitDenied = false;
        await Assert.ThrowsAnyAsync<Exception>(async () => await scenario.RunAsync<bool>(async (scope, token) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var stale = await manager.FindByIdAsync(family.Predecessor.GetTokenId()!, token);
            Assert.NotNull(stale);
            await RedeemAsync(scenario, family);
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            Assert.Same(stale, await manager.FindByIdAsync(family.Predecessor.GetTokenId()!, token));
            var context = await NativeRefreshStages.SignInAsync(scope, family.Predecessor);
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRejected);
            nativeReturned = true;
            var failure = await Record.ExceptionAsync(() => transaction.CommitAsync(token));
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal(ProtocolStorageOwner.Unavailable().Message, failure.Message);
            Assert.False(token.IsCancellationRequested);
            commitDenied = true;
            throw failure;
        }));
        Assert.Equal(1, scenario.Concurrency.Observed);
        Assert.True(nativeReturned);
        Assert.True(commitDenied);
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Redeemed, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
        var control = await NativeRefreshRows.CreateAsync(scenario);
        Assert.True(await RedeemAsync(scenario, control));
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, (await NativeRefreshRows.StatusAsync(scenario, control)).Predecessor);
    }

    /// <summary>A native catch after an actual provider update cannot conceal poisoned ownership or publish committed family denial.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PostUpdateRevocationFailure_When_NativeValidationCatchesIt_Then_OwnerRejectsCommitAndRestoredControlSucceeds(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario, redeemed: true);
        var before = await NativeRefreshRows.StatusAsync(scenario, family);
        var nativeRejected = false;
        var commitDenied = false;
        scenario.Fault.Armed = true;
        await Assert.ThrowsAnyAsync<Exception>(async () => await scenario.RunAsync<bool>(async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var context = await NativeRefreshStages.ValidateAsync(scope, family.Predecessor);
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
            nativeRejected = context.IsRejected;
            var failure = await Record.ExceptionAsync(() => transaction.CommitAsync(token));
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal(ProtocolStorageOwner.Unavailable().Message, failure.Message);
            Assert.False(token.IsCancellationRequested);
            commitDenied = true;
            throw failure;
        }));
        Assert.True(scenario.Fault.Reached);
        Assert.True(nativeRejected);
        Assert.True(commitDenied);
        Assert.Equal(before, await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.True(await scenario.CommitAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Predecessor)).IsRejected));
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Revoked),
            await NativeRefreshRows.StatusAsync(scenario, family));
    }

    /// <summary>Commits the competing owner's genuine native redemption before the stale scope attempts its write.</summary>
    private static Task<bool> RedeemAsync(NativeRefreshFixture scenario, NativeRefreshFamily family) => scenario.CommitAsync(async (scope, _) =>
    {
        var context = await NativeRefreshStages.SignInAsync(scope, family.Predecessor);
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
        Assert.False(context.IsRejected);
        return true;
    });

    /// <summary>Requires the deterministic native concurrency conflict on both real providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}
