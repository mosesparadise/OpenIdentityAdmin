using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Proves one deny-only client authentication after durable consumption and actual disposal; it never authorizes issuance or a family target.</summary>
internal sealed class DurableCleanupClientAssertion
{
    private readonly CleanupAssertionRequest request;
    private readonly string application;
    private readonly string resource;
    private readonly long revision;
    private readonly long consumedAt;
    private OpenIddictServerEvents.HandleRevocationRequestContext? continuation;
    private ProtocolStorageOwner? owner;
    private ProtocolStoragePhase? phase;
    private int claimed;

    /// <summary>Creates a receipt only from completed child consumption without retaining its provider or transaction.</summary>
    private DurableCleanupClientAssertion(CleanupAssertionRequest request, AdministratorCleanupRegistration registration, long consumedAt)
    {
        this.request = request;
        application = registration.ApplicationId;
        resource = registration.ScopeId;
        revision = registration.Revision;
        this.consumedAt = consumedAt;
    }

    /// <summary>Commits in an independent child before any target operation; replay, unknown commit and failed actual disposal yield no receipt.</summary>
    internal static async ValueTask<DurableCleanupClientAssertion?> ConsumeAsync(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy,
        OpenIddictServerEvents.ProcessAuthenticationContext context, ClientAssertionProof proof)
    {
        var request = new CleanupAssertionRequest(context, proof);
        var consumed = await ProtocolStorageChild.RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            NativeErrorOrigin.RecordPhase(context.Transaction, scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var binding = await AdministratorRegistration.ReadCleanupLockedAsync(scope, policy, cancellation);
            var watermark = binding is not null && request.Allows(context.Transaction, binding.ObservedAt)
                ? await AdministratorAssertionLedger.ReserveCleanupLockedAsync(scope.Owner, proof.Assertion, binding, cancellation) : null;
            if (watermark is not null)
            {
                await transaction.CommitAsync(cancellation);
            }
            return (Binding: binding, Watermark: watermark);
        });
        return consumed is { Binding: { } registration, Watermark: { } watermark } && request.Allows(context.Transaction, watermark)
            ? new(request, registration, watermark) : null;
    }

    /// <summary>Claims once for one fresh live revocation epoch; a repeated or foreign claim poisons the original request.</summary>
    internal void Claim(ProtocolStorageScope scope, OpenIddictServerEvents.HandleRevocationRequestContext context)
    {
        if (Interlocked.Exchange(ref claimed, 1) != 0 || !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            scope.Owner.Transaction is null || !Active(context) || !request.Allows(context.Transaction, consumedAt))
        {
            request.Fail(scope.Owner);
            throw ProtocolStorageOwner.Unavailable();
        }
        owner = scope.Owner;
        phase = owner.Capture(write: true);
        continuation = context;
    }

    /// <summary>Rechecks exact proof and registration inside the claimed epoch; target permission, family ownership and response publication remain separate.</summary>
    internal bool IsCurrent(ProtocolStorageScope scope, OpenIddictServerEvents.HandleRevocationRequestContext context,
        AdministratorCleanupRegistration current, CancellationToken token)
    {
        current.RequireCurrent(scope.Owner, token);
        if (!ReferenceEquals(owner, scope.Owner) || phase is null || !ReferenceEquals(continuation, context) || !Active(context) ||
            !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) || current.ApplicationId != application ||
            current.ScopeId != resource || current.Revision != revision)
        {
            return false;
        }
        owner.Check(phase, token);
        return request.Allows(context.Transaction, Math.Max(consumedAt, current.ObservedAt));
    }

    /// <summary>A rejected, handled or skipped native continuation cannot reuse the authenticated client-proof receipt.</summary>
    private static bool Active(OpenIddictServerEvents.HandleRevocationRequestContext context) =>
        !context.IsRejected && !context.IsRequestHandled && !context.IsRequestSkipped;
}
