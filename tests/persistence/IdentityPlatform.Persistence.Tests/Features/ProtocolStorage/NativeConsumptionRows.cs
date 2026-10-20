using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Changes native persisted state independently of the protected principal to exercise fresh-row binding guards.</summary>
internal static class NativeConsumptionRows
{
    private const string ChangedSubject = "changed-native-subject";
    private const string ApplicationForeignKey = "ApplicationId";
    private const string AuthorizationForeignKey = "AuthorizationId";

    /// <summary>Proves fresh row type, subject, application and authorization binding before any durable consumption.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, DatabaseStore store, CancellationToken token)
    {
        var type = store == DatabaseStore.Sessions ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken :
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;
        await VerifyChangedAsync(root, type, (_, row) => row.Type = row.Type!.ToUpperInvariant(), false, token);
        await VerifyChangedAsync(root, type, (_, row) => row.Subject = ChangedSubject, false, token);
        if (store == DatabaseStore.Platform)
        {
            await VerifyChangedAsync(root, type, (db, row) => db.Entry(row).Property(ApplicationForeignKey).CurrentValue = null, false, token);
            await VerifyChangedAsync(root, type, (db, row) => db.Entry(row).Property(AuthorizationForeignKey).CurrentValue = null, false, token);
            await VerifyChangedAsync(root, type, (db, row) => db.Entry(row).Property(AuthorizationForeignKey).CurrentValue = null, true, token);
            await VerifyRevokedAuthorizationAsync(root, token);
        }
    }

    /// <summary>Seeds a changed persisted binding and requires rejection with no alteration of that row's valid status.</summary>
    private static async Task VerifyChangedAsync(IServiceProvider root, string type, Action<DbContext, OpenIddictEntityFrameworkCoreToken> update,
        bool removePrincipalAuthorization, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
        await WriteAsync(root, principal, async (scope, cancellation) =>
        {
            await scope.Owner.RunAsync(async current =>
            {
                var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync(row => row.Id == principal.GetTokenId(), current);
                update(scope.Owner.Database, row);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, cancellation, write: true);
        }, token);
        if (removePrincipalAuthorization)
        {
            principal.SetAuthorizationId(null);
        }
        Assert.False(await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Valid, token);
        await WriteAsync(root, principal, async (scope, cancellation) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var row = await manager.FindByIdAsync(principal.GetTokenId()!, cancellation);
            Assert.NotNull(row);
            await manager.DeleteAsync(row, cancellation);
        }, token);
    }

    /// <summary>An otherwise valid code cannot be redeemed after its native authorization has been revoked.</summary>
    private static async Task VerifyRevokedAuthorizationAsync(IServiceProvider root, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        await WriteAsync(root, principal, async (scope, cancellation) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorization = await manager.FindByIdAsync(principal.GetAuthorizationId()!, cancellation);
            Assert.True(await manager.TryRevokeAsync(authorization!, cancellation));
        }, token);
        Assert.False(await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Commits a fixture mutation under the same owning-store lock using an independent fresh context.</summary>
    private static Task<bool> WriteAsync(IServiceProvider root, ClaimsPrincipal principal,
        Func<ProtocolStorageScope, CancellationToken, Task> update, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            if (principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken))
            {
                await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            }
            else
            {
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            }
            await update(scope, cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });
}
