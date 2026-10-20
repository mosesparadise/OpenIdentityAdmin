using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves actual native consumption remains durable across later failure using independent owned database observations.</summary>
internal static class NativeConsumptionConformance
{
    private const string RolledBackScope = "consumption-later-rollback";
    private const string MissingToken = "missing-native-consumption-token";

    /// <summary>Runs the same code/PAR/state failure and stale-validation controls on each real provider.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, DatabaseStore store, CancellationToken token)
    {
        await NativeConsumptionSeed.InitializeAsync(root, store, token);
        string[] types = store == DatabaseStore.Sessions ? [OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken] :
            [OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken];
        foreach (var type in types)
        {
            var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
            await VerifyLaterRollbackAsync(root, principal, token);
            await RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
            Assert.False(await ConsumeAsync(root, principal, token));
            await RequireStatusAsync(root, principal, type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode ?
                OpenIddictConstants.Statuses.Revoked : OpenIddictConstants.Statuses.Redeemed, token);
            principal.SetTokenId(MissingToken);
            Assert.False(await ConsumeAsync(root, principal, token));
        }
        if (store == DatabaseStore.Platform)
        {
            await VerifyCachedRequestAsync(root, token);
        }
        else
        {
            var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, token);
            Assert.False(await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
                (scope, _) => NativeConsumptionDispatch.ClientAsync(scope, principal, OpenIddictConstants.Errors.AccessDenied)));
            await RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
            Assert.False(await ConsumeAsync(root, principal, token));
        }
    }

    /// <summary>A real native redemption commits before a later child writes and throws, and that later write must roll back.</summary>
    private static async Task VerifyLaterRollbackAsync(IServiceProvider root, ClaimsPrincipal principal, CancellationToken token)
    {
        var factory = root.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token);
        var expected = new ArithmeticException();
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => request.RunAsync(factory, async (scope, cancellation) =>
        {
            Assert.True(await NativeConsumptionDispatch.RunAsync(scope, principal));
            return await ProtocolStorageChild.RunAsync<int>(factory, async (child, childToken) =>
            {
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                await child.Services.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(new OpenIddictScopeDescriptor { Name = RolledBackScope }, childToken);
                throw expected;
            });
        })));
        await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(factory, async (scope, cancellation) =>
        {
            Assert.Null(await scope.Services.GetRequiredService<IOpenIddictScopeManager>().FindByNameAsync(RolledBackScope, cancellation));
            return true;
        });
    }

    /// <summary>The native cached authorization request cannot be substituted for a pushed authorization reference.</summary>
    private static async Task VerifyCachedRequestAsync(IServiceProvider root, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, token);
        principal.SetClaim(OpenIddictConstants.Claims.Private.RequestTokenType, OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest);
        Assert.False(await ConsumeAsync(root, principal, token));
        await RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Runs one native consumption attempt within a new original request and actual owned parent dispatcher.</summary>
    internal static Task<bool> ConsumeAsync(IServiceProvider root, ClaimsPrincipal principal, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
            (scope, _) => NativeConsumptionDispatch.RunAsync(scope, principal));

    /// <summary>Reads committed native status through a different owned context rather than the consumption tracker.</summary>
    internal static Task RequireStatusAsync(IServiceProvider root, ClaimsPrincipal principal, string status, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByIdAsync(principal.GetTokenId()!, cancellation);
            Assert.NotNull(entry);
            Assert.True(await manager.HasStatusAsync(entry, status, cancellation));
            return true;
        });
}
