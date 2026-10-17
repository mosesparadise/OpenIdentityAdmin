using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Checks durable receipt authority at the gap between committed consumption and a fresh locked issuance epoch.</summary>
internal static class NativeConsumptionReceipts
{
    private const string ChangedSubject = "receipt-subject-substitution";

    /// <summary>Runs identical real-provider controls for replay, revision, protected claim and expiry changes before reacquisition.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        await VerifyCommitMutationAsync(first, token);
        await VerifyDuplicateClaimAsync(first, token);
        await VerifyForeignScopeAsync(first, token);
        foreach (var scenario in Enum.GetValues<ReceiptScenario>())
        {
            var principal = await NativeConsumptionSeed.TokenAsync(first, scenario == ReceiptScenario.PushedRequest ?
                OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken : OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
            await new ProtocolStorageRequest(first.GetRequiredService<TimeProvider>(), token).RunAsync(first.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                var context = await NativeConsumptionDispatch.ServerContextAsync(scope, principal);
                Assert.False(context.IsRejected);
                var receipt = context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!);
                Assert.NotNull(receipt);
                await ChangeGapAsync(second, principal, scenario, cancellation);
                var accepted = await ProtocolStorageChild.RunAsync(first.GetRequiredService<IServiceScopeFactory>(), async (child, childToken) =>
                {
                    await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                    await AdministratorProtocolLocks.AcquireProfileAsync(child.Owner, childToken);
                    Assert.False(await receipt.IsCurrentAsync(child, context, childToken));
                    receipt.Claim(child, context);
                    Assert.False(await receipt.IsCurrentAsync(child, new OpenIddictServerEvents.ProcessSignInContext(context.Transaction), childToken));
                    var current = await receipt.IsCurrentAsync(child, context, childToken);
                    await transaction.CommitAsync(childToken);
                    return current;
                });
                Assert.Equal(scenario is ReceiptScenario.Unchanged or ReceiptScenario.PushedRequest, accepted);
                return true;
            });
        }
    }

    /// <summary>An active standalone owner cannot claim an original-request receipt while that original ambient request remains current.</summary>
    private static async Task VerifyForeignScopeAsync(IServiceProvider root, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token)
            .RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, _) =>
            {
                var context = await NativeConsumptionDispatch.ServerContextAsync(scope, principal);
                var receipt = context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!)!;
                await using var foreign = await ProtocolStorageScope.OpenAsync(root.GetRequiredService<IServiceScopeFactory>(), token);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(foreign.Owner);
                Assert.Same(scope, ProtocolStorageAmbient.Current!.Scope);
                Assert.Throws<InvalidOperationException>(() => receipt.Claim(foreign, context));
                return true;
            }));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
    }

    /// <summary>A post-commit mutation cannot become the captured original proof even though native redemption stays durable.</summary>
    private static async Task VerifyCommitMutationAsync(IServiceProvider root, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        root.GetRequiredService<NativeConsumptionCommit>().AfterCommit = () => principal.SetClaim(OpenIddictConstants.Claims.Subject, ChangedSubject);
        try
        {
            await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, _) =>
            {
                var context = await NativeConsumptionDispatch.ServerContextAsync(scope, principal);
                Assert.True(context.IsRejected);
                Assert.Null(context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!));
                return true;
            });
        }
        finally { root.GetRequiredService<NativeConsumptionCommit>().AfterCommit = null; }
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
    }

    /// <summary>Catching a duplicate receipt claim cannot recover the original request or undo its earlier durable consumption.</summary>
    private static async Task VerifyDuplicateClaimAsync(IServiceProvider root, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token)
            .RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, _) =>
            {
                var context = await NativeConsumptionDispatch.ServerContextAsync(scope, principal);
                var receipt = context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!)!;
                return await ProtocolStorageChild.RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (child, childToken) =>
                {
                    await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                    await AdministratorProtocolLocks.AcquireProfileAsync(child.Owner, childToken);
                    receipt.Claim(child, context);
                    Assert.Throws<InvalidOperationException>(() => receipt.Claim(child, context));
                    return true;
                });
            }));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
    }

    /// <summary>Commits a competing change in an independent request after the first redemption and before issuance reacquisition.</summary>
    private static async Task ChangeGapAsync(IServiceProvider root, ClaimsPrincipal principal, ReceiptScenario scenario, CancellationToken token)
    {
        if (scenario == ReceiptScenario.ProtectedClaims)
        {
            principal.SetClaim(OpenIddictConstants.Claims.Subject, ChangedSubject);
        }
        else if (scenario == ReceiptScenario.Replay)
        {
            Assert.False(await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
                (scope, _) => NativeConsumptionDispatch.ValidateAsync(scope, principal)));
            await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Revoked, token);
        }
        else if (scenario is ReceiptScenario.ProfileRevision or ReceiptScenario.Expiration or ReceiptScenario.Authorization)
        {
            await ChangeStoredAsync(root, principal, scenario, token);
        }
    }

    /// <summary>Uses the same owning profile lock and installed native managers for the independent committed mutation.</summary>
    private static Task<bool> ChangeStoredAsync(IServiceProvider root, ClaimsPrincipal principal, ReceiptScenario scenario, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            if (scenario == ReceiptScenario.ProfileRevision)
            {
                await scope.Owner.RunAsync(async current =>
                {
                    var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current);
                    profile.Revision++;
                    await scope.Owner.Database.SaveChangesAsync(current);
                }, cancellation, write: true);
            }
            else if (scenario == ReceiptScenario.Authorization)
            {
                var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
                Assert.True(await manager.TryRevokeAsync((await manager.FindByIdAsync(principal.GetAuthorizationId()!, cancellation))!, cancellation));
            }
            else
            {
                var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
                var entry = (await manager.FindByIdAsync(principal.GetTokenId()!, cancellation))!;
                var descriptor = new OpenIddictTokenDescriptor();
                await manager.PopulateAsync(descriptor, entry, cancellation);
                descriptor.ExpirationDate = scope.Owner.Clock.GetUtcNow();
                await manager.UpdateAsync(entry, descriptor, cancellation);
            }
            await transaction.CommitAsync(cancellation);
            return true;
        });

    private enum ReceiptScenario
    {
        Unchanged, PushedRequest, Replay, ProfileRevision, ProtectedClaims, Expiration, Authorization
    }
}
