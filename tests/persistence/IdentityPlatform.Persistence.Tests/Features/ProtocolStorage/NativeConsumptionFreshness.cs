using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Requires a previously accepted redemption continuation to detect persisted drift and changed native outcomes before issuance.</summary>
internal static class NativeConsumptionFreshness
{
    private const string ChangedSubject = "changed-consumption-subject";

    /// <summary>Loads real tracked native rows first, then changes persisted state without row-count changes in the same locked transaction.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, CancellationToken token)
    {
        foreach (var change in Enum.GetValues<Change>())
        {
            var principal = await NativeConsumptionSeed.TokenAsync(root, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
            await new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, _) =>
            {
                var context = await NativeConsumptionDispatch.ServerContextAsync(scope, principal);
                Assert.False(context.IsRejected);
                var receipt = context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!)!;
                await ProtocolStorageChild.RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (child, cancellation) =>
                {
                    await using var transaction = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                    await AdministratorProtocolLocks.AcquireProfileAsync(child.Owner, cancellation);
                    receipt.Claim(child, context);
                    Assert.True(await receipt.IsCurrentAsync(child, context, cancellation));
                    var hook = root.GetRequiredService<NativeConsumptionRead>();
                    var observed = 0;
                    if (change >= Change.AwaitedRequest)
                    {
                        hook.AfterAuthorization = () => { observed++; ChangeContext(context, change); };
                    }
                    else
                    {
                        await ChangeAsync(child, principal, context, change, cancellation);
                    }
                    try
                    {
                        Assert.False(await receipt.IsCurrentAsync(child, context, cancellation));
                    }
                    finally { hook.AfterAuthorization = null; }
                    Assert.Equal(change >= Change.AwaitedRequest ? 1 : 0, observed);
                    return true;
                });
                return true;
            });
            await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
        }
    }

    /// <summary>Seeds either one persisted row mutation or one exact native continuation mutation after the positive observation.</summary>
    private static async Task ChangeAsync(ProtocolStorageScope scope, ClaimsPrincipal principal,
        OpenIddictServerEvents.ProcessSignInContext context, Change change, CancellationToken token)
    {
        if (change <= Change.AuthorizationBinding)
        {
            Assert.Equal(1, await scope.Owner.RunAsync(current => ChangeStoredAsync(scope, principal, change, current), token, write: true));
            return;
        }
        ChangeContext(context, change);
    }

    /// <summary>Changes the exact live native continuation, including in-place mutation which preserves the request object.</summary>
    private static void ChangeContext(OpenIddictServerEvents.ProcessSignInContext context, Change change)
    {
        switch (change)
        {
            case Change.AwaitedClient:
                context.Request.ClientId = ChangedSubject;
                break;
            case Change.Request:
            case Change.AwaitedRequest:
                context.Transaction.Request = new(context.Request.GetParameters());
                break;
            case Change.Endpoint:
            case Change.AwaitedEndpoint:
                context.Transaction.EndpointType = OpenIddictServerEndpointType.Authorization;
                break;
            case Change.Rejected:
            case Change.AwaitedRejected:
                context.Reject();
                break;
            case Change.Skipped:
            case Change.AwaitedSkipped:
                context.SkipRequest();
                break;
            case Change.Authentication:
            case Change.AwaitedAuthentication:
                var original = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!)!;
                context.Transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!,
                    new OpenIddictServerEvents.ProcessAuthenticationContext(context.Transaction) { AuthorizationCodePrincipal = original.AuthorizationCodePrincipal });
                break;
        }
    }

    /// <summary>Bypasses EF tracking only for the intentional fixture mutation so final acceptance must reread actual persisted state.</summary>
    private static async ValueTask<int> ChangeStoredAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, Change change, CancellationToken token)
    {
        var tokenId = principal.GetTokenId();
        var authorizationId = principal.GetAuthorizationId();
        var tokens = scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Id == tokenId);
        var authorizations = scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(row => row.Id == authorizationId);
        return change switch
        {
            Change.TokenStatus => await tokens.ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, OpenIddictConstants.Statuses.Valid), token),
            Change.TokenExpiry => await tokens.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpirationDate, scope.Owner.Clock.GetUtcNow().UtcDateTime), token),
            Change.TokenBinding => await tokens.ExecuteUpdateAsync(update => update.SetProperty(row => row.Subject, ChangedSubject), token),
            Change.AuthorizationStatus => await authorizations.ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, OpenIddictConstants.Statuses.Revoked), token),
            Change.AuthorizationBinding => await authorizations.ExecuteUpdateAsync(update => update.SetProperty(row => row.Subject, ChangedSubject), token),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
    }

    /// <summary>Enumerates independently exercised final-authority defects after a genuine positive receipt check.</summary>
    private enum Change
    {
        TokenStatus, TokenExpiry, TokenBinding, AuthorizationStatus, AuthorizationBinding, Request, Endpoint, Rejected, Skipped, Authentication,
        AwaitedRequest, AwaitedEndpoint, AwaitedRejected, AwaitedSkipped, AwaitedAuthentication, AwaitedClient
    }
}
