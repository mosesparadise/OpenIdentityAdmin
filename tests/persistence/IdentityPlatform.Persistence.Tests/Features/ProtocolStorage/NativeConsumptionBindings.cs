using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Rejects fresh binding mismatches without consuming or revoking an unrelated native token.</summary>
internal static class NativeConsumptionBindings
{
    private const string OtherSubject = "other-consumption-subject";
    private const string OtherApplication = "other-consumption-application";

    /// <summary>Checks protected principal bindings on each store and authoritative registration availability on the server.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, DatabaseStore store, CancellationToken token)
    {
        var type = store == DatabaseStore.Sessions ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken :
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;
        var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
        var other = await NativeConsumptionSeed.TokenAsync(root, type, token);
        var altered = Clone(principal).SetAuthorizationId(other.GetAuthorizationId() ?? OtherApplication);
        await RequireDeniedAsync(root, altered, principal, token);
        if (store == DatabaseStore.Platform)
        {
            await RequireDeniedAsync(root, Clone(principal).SetClaim(OpenIddictConstants.Claims.Subject, OtherSubject), principal, token);
            await RequireDeniedAsync(root, Clone(principal).SetPresenters(OtherApplication), principal, token);
            await RequireDeniedAsync(root, Clone(principal).SetAuthorizationId(null), principal, token);
            await VerifyProfileAsync(root, principal, token);
        }
        Assert.True(await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, other, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Disabled or differently bound profile state cannot authorize redemption of a previously validated code.</summary>
    private static async Task VerifyProfileAsync(IServiceProvider root, ClaimsPrincipal principal, CancellationToken token)
    {
        var original = await ChangeProfileAsync(root, profile => profile.Enabled = false, token);
        await RequireDeniedAsync(root, principal, principal, token);
        await ChangeProfileAsync(root, profile => { profile.Enabled = true; profile.ApplicationId = OtherApplication; }, token);
        await RequireDeniedAsync(root, principal, principal, token);
        await ChangeProfileAsync(root, profile => profile.ApplicationId = original.ApplicationId, token);
        await VerifyMissingProfileAsync(root, principal, token);
    }

    /// <summary>A missing coordination row fails closed and a live request never recreates registration state.</summary>
    private static async Task VerifyMissingProfileAsync(IServiceProvider root, ClaimsPrincipal principal, CancellationToken token)
    {
        var factory = root.GetRequiredService<IServiceScopeFactory>();
        var original = await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var profile = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current), cancellation);
            scope.Owner.Database.Remove(profile);
            await scope.Owner.Database.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return profile;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            Assert.False(await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<AdministratorProtocolProfile>().AnyAsync(current), cancellation));
            scope.Owner.Database.Add(original);
            await scope.Owner.Database.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Changes current profile state in a distinct committed context while retaining the original values independently.</summary>
    private static Task<AdministratorProtocolProfile> ChangeProfileAsync(IServiceProvider root, Action<AdministratorProtocolProfile> update, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var original = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<AdministratorProtocolProfile>()
                .AsNoTracking().SingleAsync(current), cancellation);
            var profile = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current), cancellation);
            update(profile);
            await scope.Owner.Database.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return original;
        });

    /// <summary>Requires rejection and verifies that the independently read native row remains usable.</summary>
    private static async Task RequireDeniedAsync(IServiceProvider root, ClaimsPrincipal altered, ClaimsPrincipal original, CancellationToken token)
    {
        Assert.False(await NativeConsumptionConformance.ConsumeAsync(root, altered, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, original, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Copies protected claims so a negative control cannot silently mutate its independent baseline principal.</summary>
    private static ClaimsPrincipal Clone(ClaimsPrincipal principal) => new(new ClaimsIdentity(principal.Claims));
}
