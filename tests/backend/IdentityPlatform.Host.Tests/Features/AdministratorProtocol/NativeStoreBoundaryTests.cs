using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Characterizes native manager behavior that the B2 cancellation/transaction owner must preserve.</summary>
public sealed class NativeStoreBoundaryTests
{
    /// <summary>Proves native additional filtering calls a base relationship getter between query yields and disposes on failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeRelationshipFiltering_When_Enumerating_Then_GettersRunBeforeReaderDisposal(bool authorization)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var store = (NativeStoreProbe)scope.ServiceProvider.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        var token = TestContext.Current.CancellationToken;
        var rows = await CollectAsync(manager, authorization, token);
        var match = Assert.IsType<OpenIddictEntityFrameworkCoreToken>(Assert.Single(rows));
        Assert.Equal(NativeStoreProbe.ExactIdentifier, authorization ? match.Authorization!.Id : match.Application!.Id);
        Assert.Equal(2, store.GetterCalls);
        Assert.Equal(1, store.Disposals);
        Assert.False(store.EnumerationActive);
        store.RejectGetterDuringEnumeration = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CollectAsync(manager, authorization, token));
        Assert.Equal(3, store.GetterCalls);
        Assert.Equal(2, store.Disposals);
        Assert.False(store.EnumerationActive);
    }

    /// <summary>Proves cancellation can become native false; an outer owner must still reject commit instead of relying on an escaping exception.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CancelledNativeUpdate_When_RedeemingOrRevoking_Then_ManagerReturnsFalseAndCancellationRemains(bool revoke)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var store = (NativeStoreProbe)scope.ServiceProvider.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        store.CancelUpdate = request;
        var token = new OpenIddictEntityFrameworkCoreToken
        {
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid
        };
        var result = revoke ? await manager.TryRevokeAsync(token, request.Token) : await manager.TryRedeemAsync(token, request.Token);
        Assert.False(result);
        Assert.Equal(1, store.UpdateCalls);
        Assert.True(request.IsCancellationRequested);
        Assert.True(store.CancellationThrown);
        Assert.Equal(request.Token, store.ReceivedToken);
        Assert.Equal(request.Token, store.ThrownToken);
        Assert.Throws<OperationCanceledException>(() => request.Token.ThrowIfCancellationRequested());
    }

    /// <summary>Uses the installed native manager with caching disabled and additional ordinal filtering left enabled.</summary>
    private static ServiceProvider Provider()
    {
        const string Connection = "Host=localhost;Database=synthetic_native_boundary";
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, Connection);
        services.AddOpenIddict().AddCore(core => core.ReplaceTokenStore<OpenIddictEntityFrameworkCoreToken, NativeStoreProbe>());
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Fully consumes the actual native manager enumeration without buffering or altering its store-side filter.</summary>
    private static async Task<List<object>> CollectAsync(IOpenIddictTokenManager manager, bool authorization, CancellationToken token)
    {
        var source = authorization ? manager.FindByAuthorizationIdAsync(NativeStoreProbe.ExactIdentifier, token) :
            manager.FindByApplicationIdAsync(NativeStoreProbe.ExactIdentifier, token);
        List<object> rows = [];
        await foreach (var row in source.WithCancellation(token))
        {
            rows.Add(row);
        }
        return rows;
    }
}
