using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Authorizes one original continuation only after assertion consumption commits and its child actually cleans up.</summary>
internal sealed class DurableClientAssertion
{
    private readonly OpenIddictServerEvents.ProcessAuthenticationContext authentication;
    private readonly ClientAssertionProof proof;
    private readonly ProtocolStorageRequest request;
    private readonly AdministratorRegistrationBinding registration;
    private readonly long consumedAt;
    private readonly OpenIddictRequest nativeRequest;
    private readonly OpenIddictServerEndpointType endpoint;
    private OpenIddictServerEvents.ProcessSignInContext? issuance;
    private ProtocolStorageOwner? owner;
    private ProtocolStoragePhase? phase;
    private int claimed;

    /// <summary>Retains immutable registration and consumed-time bounds without retaining the child provider or database.</summary>
    private DurableClientAssertion(OpenIddictServerEvents.ProcessAuthenticationContext authentication, ClientAssertionProof proof,
        ProtocolStorageRequest request, AdministratorRegistrationBinding registration, long consumedAt,
        OpenIddictRequest nativeRequest, OpenIddictServerEndpointType endpoint)
    {
        this.authentication = authentication;
        this.proof = proof;
        this.request = request;
        this.registration = registration;
        this.consumedAt = consumedAt;
        this.nativeRequest = nativeRequest;
        this.endpoint = endpoint;
    }

    /// <summary>Creates no receipt on replay, expired proof, unknown commit or failed actual cleanup; consumed proofs are never restored.</summary>
    internal static async ValueTask<DurableClientAssertion?> ConsumeAsync(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy,
        OpenIddictServerEvents.ProcessAuthenticationContext context, ClientAssertionProof proof)
    {
        var original = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        var nativeRequest = context.Request;
        var endpoint = context.EndpointType;
        var consumed = await ProtocolStorageChild.RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            NativeErrorOrigin.RecordPhase(context.Transaction, scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var binding = proof.IsCurrent(context) ? await AdministratorRegistration.ReadLockedAsync(scope, policy, cancellation) : null;
            var watermark = binding is not null ? await AdministratorAssertionLedger.ReserveLockedAsync(scope.Owner, proof.Assertion, cancellation) : null;
            if (watermark is not null)
            {
                await transaction.CommitAsync(cancellation);
            }
            return (Binding: binding, Watermark: watermark);
        });
        if (consumed is not { Binding: { } registration, Watermark: { } watermark })
        {
            return null;
        }
        var receipt = new DurableClientAssertion(context, proof, original, registration, watermark, nativeRequest, endpoint);
        return receipt.Allows(context.Transaction, watermark) ? receipt : null;
    }

    /// <summary>Claims exactly one fresh issuance epoch; a repeated or foreign claim faults the original request even if caught.</summary>
    internal void Claim(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (Interlocked.Exchange(ref claimed, 1) != 0 || !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            scope.Owner.Transaction is null || context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped || !Allows(context.Transaction, consumedAt))
        {
            request.Fail();
            scope.Owner.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        owner = scope.Owner;
        phase = owner.Capture(write: true);
        issuance = context;
    }

    /// <summary>Rechecks proof, strict expiry and the just-read registration while the same profile lock and issuance epoch remain held.</summary>
    internal bool IsCurrent(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context,
        AdministratorRegistrationBinding current, CancellationToken token)
    {
        if (!ReferenceEquals(owner, scope.Owner) || phase is null || !ReferenceEquals(issuance, context) || context.IsRejected || context.IsRequestSkipped ||
            !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            current.ApplicationId != registration.ApplicationId || current.ScopeId != registration.ScopeId || current.Revision != registration.Revision)
        {
            return false;
        }
        owner.Check(phase, token);
        return Allows(context.Transaction, Math.Max(consumedAt, current.ObservedAt));
    }

    /// <summary>Uses a nondecreasing profile clock and preserves the original native validation result and authentication context.</summary>
    private bool Allows(OpenIddictServerTransaction transaction, long watermark)
    {
        request.RequireCurrent();
        var sample = request.Clock.GetUtcNow().ToUnixTimeSeconds();
        return ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && ReferenceEquals(authentication.Transaction, transaction) &&
            ReferenceEquals(nativeRequest, transaction.Request) && transaction.EndpointType == endpoint &&
            proof.IsCurrent(authentication) && sample >= 0 && watermark >= 0 &&
            watermark - sample <= AdministratorAssertion.ClockSkewSeconds && proof.Assertion.Allows(Math.Max(sample, watermark));
    }
}
