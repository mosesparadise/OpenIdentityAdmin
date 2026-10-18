using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Verifies actual native row capacity, partial rollback, independent competitors and unknown commit on each real provider.</summary>
internal static class NativeTokenBudgets
{
    private const int TokenLimit = 20000;
    private const int AuthorizationLimit = 2000;
    private const int RetainedRequestLimit = 10000;
    private const int LiveRequestLimit = 128;
    private const int ExchangeRows = 2;

    /// <summary>Runs retained/live category boundaries using the same production quota helper and actual native manager writes.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, NativeLockObservation observer, CancellationToken token)
    {
        var clock = first.GetRequiredService<NativeConsumptionClock>();
        var other = second.GetRequiredService<NativeConsumptionClock>();
        clock.Set(await NativeTokenBudgetFixture.ObserveAsync(first, token));
        other.Set(clock.GetUtcNow());
        try
        {
            await NativeTokenBudgetFixture.ClearAsync(first, token);
            await NativeTokenBudgetFixture.FillAsync(first, TokenLimit - ExchangeRows, false, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.Statuses.Valid, token);
            Assert.False(await ExchangeAsync(first, true, token));
            Assert.Equal((TokenLimit - ExchangeRows, 0), await NativeTokenBudgetFixture.CountAsync(second, token));
            await NativeTokenBudgetRace.VerifyAsync(first, second, observer, token);
            Assert.Equal((TokenLimit, 0), await NativeTokenBudgetFixture.CountAsync(first, token));
            Assert.False(await ExchangeAsync(second, false, token));
            await VerifyUnknownCommitAsync(first, second, token);
            await VerifyCategoryCapsAsync(first, second, token);
            await NativeTokenCleanup.VerifyAsync(first, second, token);
        }
        finally { clock.Reset(); other.Reset(); await NativeTokenBudgetFixture.ClearAsync(first, token); }
    }

    /// <summary>A partial native creation must roll back; exact creation commits only after authoritative row reconciliation.</summary>
    private static Task<bool> ExchangeAsync(IServiceProvider root, bool partial, CancellationToken token) => NativeTokenBudgetFixture.WriteAsync(root, async (scope, cancellation) =>
    {
        var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
        var budget = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, application, AdministratorTokenCreation.TokenExchange, TimeSpan.Zero, cancellation);
        if (budget is null)
        {
            return (false, false);
        }
        if (!budget.HasCapacity)
        {
            return (false, await budget.CanCommitCleanupAsync(cancellation));
        }
        await NativeTokenBudgetFixture.CreateTokensAsync(scope, application, partial ? 1 : ExchangeRows, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, cancellation);
        var current = await budget.IsCurrentAsync(cancellation);
        return (current, current);
    }, token);

    /// <summary>Lost acknowledgement retains exactly the committed native rows and never resubmits creation automatically.</summary>
    private static async Task VerifyUnknownCommitAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        var expected = new ArithmeticException();
        var attempts = 0;
        first.GetRequiredService<NativeConsumptionCommit>().AfterCommit = () => { attempts++; throw expected; };
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => ExchangeAsync(first, false, token)));
        }
        finally { first.GetRequiredService<NativeConsumptionCommit>().AfterCommit = null; }
        Assert.Equal(1, attempts);
        Assert.Equal((ExchangeRows, 0), await NativeTokenBudgetFixture.CountAsync(second, token));
    }

    /// <summary>Authorization, retained-request and live-request caps independently deny affected creation without evicting live rows.</summary>
    private static async Task VerifyCategoryCapsAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.FillAsync(first, AuthorizationLimit, true, OpenIddictConstants.AuthorizationTypes.AdHoc, OpenIddictConstants.Statuses.Valid, token);
        Assert.False(await CanReserveAsync(second, AdministratorTokenCreation.AuthorizationCode, token));
        Assert.True(await CanReserveAsync(second, AdministratorTokenCreation.PushedRequest, token));
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.FillAsync(first, RetainedRequestLimit, false, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, OpenIddictConstants.Statuses.Redeemed, token);
        Assert.False(await CanReserveAsync(second, AdministratorTokenCreation.PushedRequest, token));
        Assert.True(await CanReserveAsync(second, AdministratorTokenCreation.TokenExchange, token));
        Assert.Equal((RetainedRequestLimit, 0), await NativeTokenBudgetFixture.CountAsync(first, token));
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.FillAsync(first, LiveRequestLimit, false, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, OpenIddictConstants.Statuses.Valid, token);
        Assert.False(await CanReserveAsync(second, AdministratorTokenCreation.PushedRequest, token));
        Assert.Equal((LiveRequestLimit, 0), await NativeTokenBudgetFixture.CountAsync(first, token));
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.FillAsync(first, LiveRequestLimit, false,
            OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken.ToUpperInvariant(), OpenIddictConstants.Statuses.Valid.ToUpperInvariant(), token);
        Assert.False(await CanReserveAsync(second, AdministratorTokenCreation.PushedRequest, token));
    }

    /// <summary>Rolls back a read-only capacity probe; a returned capacity object alone never means native issuance completed.</summary>
    private static Task<bool> CanReserveAsync(IServiceProvider root, AdministratorTokenCreation creation, CancellationToken token) => NativeTokenBudgetFixture.WriteAsync(root, async (scope, cancellation) =>
    {
        var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
        var budget = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, application, creation, TimeSpan.Zero, cancellation);
        return (budget?.HasCapacity == true, false);
    }, token);
}
