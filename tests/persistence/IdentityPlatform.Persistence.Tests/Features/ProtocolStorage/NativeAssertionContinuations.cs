using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises accepted assertion continuations after real cryptography, durable consumption and fresh child acquisition.</summary>
internal static class NativeAssertionContinuations
{
    private const int ProofLifetime = 60;
    private const string ChangedSubject = "changed-assertion-subject";

    /// <summary>Proves positive continuation, strict expiry, changed proof/registration, foreign transaction and duplicate-claim rejection.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, NativeAssertionMaterial material, DateTimeOffset now, CancellationToken token)
    {
        foreach (var change in Enum.GetValues<ContinuationChange>())
        {
            try
            {
                var operation = NativeAssertionAuthentication.AuthenticateAsync(root, material.Sign(now.ToUnixTimeSeconds()), false, token,
                    (authentication, _) => VerifyCurrentAsync(root, material.Policy, authentication, now, change));
                if (change is ContinuationChange.ForeignTransaction or ContinuationChange.DuplicateClaim)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
                }
                else
                {
                    await operation;
                }
            }
            finally { root.GetRequiredService<NativeConsumptionClock>().Set(now); }
        }
    }

    /// <summary>Reacquires the actual profile lock in a fresh issuance transaction after the assertion child has disposed.</summary>
    private static async ValueTask VerifyCurrentAsync(IServiceProvider root, AdministratorRegistrationPolicy policy,
        OpenIddictServerEvents.ProcessAuthenticationContext authentication, DateTimeOffset now, ContinuationChange change)
    {
        Assert.False(authentication.IsRejected);
        var receipt = authentication.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!);
        Assert.NotNull(receipt);
        await ProtocolStorageChild.RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var native = change == ContinuationChange.ForeignTransaction
                ? await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync() : authentication.Transaction;
            var context = new OpenIddictServerEvents.ProcessSignInContext(native) { Principal = new ClaimsPrincipal(new ClaimsIdentity()) };
            receipt.Claim(scope, context);
            if (change == ContinuationChange.DuplicateClaim)
            {
                receipt.Claim(scope, context);
            }
            var registration = await AdministratorRegistration.ReadLockedAsync(scope, policy, cancellation);
            Assert.NotNull(registration);
            ApplyChange(root, authentication, now, change);
            if (change == ContinuationChange.Registration)
            {
                registration = registration with
                {
                    Revision = registration.Revision + 1
                };
            }
            if (change == ContinuationChange.ReplacedContext)
            {
                context = new OpenIddictServerEvents.ProcessSignInContext(native) { Principal = new ClaimsPrincipal(new ClaimsIdentity()) };
            }
            Assert.Equal(change == ContinuationChange.None, receipt.IsCurrent(scope, context, registration, cancellation));
            return true;
        });
    }

    /// <summary>Changes original proof/time after claiming without replacing the real committed receipt or its owning transaction.</summary>
    private static void ApplyChange(IServiceProvider root, OpenIddictServerEvents.ProcessAuthenticationContext authentication,
        DateTimeOffset now, ContinuationChange change)
    {
        if (change == ContinuationChange.Expiry)
        {
            root.GetRequiredService<NativeConsumptionClock>().Set(now.AddSeconds(ProofLifetime));
        }
        if (change == ContinuationChange.Proof)
        {
            authentication.ClientAssertionPrincipal!.SetClaim(OpenIddictConstants.Claims.Subject, ChangedSubject);
        }
        if (change == ContinuationChange.Endpoint)
        {
            authentication.Transaction.EndpointType = OpenIddictServerEndpointType.Token;
        }
        if (change == ContinuationChange.Request)
        {
            authentication.Transaction.Request = new OpenIddictRequest(authentication.Request.GetParameters());
        }
    }

    /// <summary>Names independent continuation boundaries without claiming complete HTTP issuance coverage.</summary>
    private enum ContinuationChange
    {
        None,
        Expiry,
        Proof,
        Registration,
        ReplacedContext,
        Endpoint,
        Request,
        ForeignTransaction,
        DuplicateClaim
    }
}
