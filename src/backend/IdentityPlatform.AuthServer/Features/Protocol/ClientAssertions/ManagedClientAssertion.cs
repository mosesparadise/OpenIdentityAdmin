using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Attaches original native signature evidence for a managed client without adopting administrator assertion authority.</summary>
internal sealed class ManagedAssertionCapture : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>Accepts exactly one signature result associated with the original token request and its native key.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Token && ManagedDemand.Candidate(context.Request?.ClientId) &&
            context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion]))
        {
            var key = typeof(ManagedAssertionCapture).FullName!;
            var proof = ClientAssertionProof.CaptureManaged(context);
            if (proof is null || context.Transaction.GetProperty<ClientAssertionProof>(key) is not null)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidClient);
            }
            else
            {
                context.Transaction.SetProperty(key, proof);
            }
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>Consumes the assertion in a committed child before native grant validation, retaining its original request and approved revision.</summary>
internal sealed class ManagedClientAssertion(IServiceScopeFactory factory) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    /// <summary>Rejects a stale or repeated proof and retains the receipt only while both original deadlines remain current after child disposal.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (context.EndpointType != OpenIddictServerEndpointType.Token || !ManagedDemand.Candidate(context.ClientId) || context.ClientAssertion is null)
        {
            return;
        }
        var proof = context.Transaction.GetProperty<ClientAssertionProof>(typeof(ManagedAssertionCapture).FullName!);
        if (proof?.Managed is not { } assertion || !proof.Claim(context) || ManagedAssertionReceipt.Find(context.Transaction) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        var stored = await ProtocolStorageChild.RunAsync(factory, (scope, token) => ApplicationAssertionLedger.ConsumeAsync(scope, assertion, token,
            owner => NativeErrorOrigin.RecordPhase(context.Transaction, owner)));
        var receipt = stored is null ? null : new ManagedAssertionReceipt(context, proof, stored);
        if (receipt is null || !receipt.IsCurrent(context.Transaction))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        context.Transaction.SetProperty(typeof(ManagedAssertionReceipt).FullName!, receipt);
    }
}

/// <summary>Binds a committed managed assertion to the native request and its original key and credential revision.</summary>
internal sealed class ManagedAssertionReceipt(OpenIddictServerEvents.ProcessAuthenticationContext authentication, ClientAssertionProof proof, ApplicationAssertionReceipt stored)
{
    private readonly OpenIddictRequest request = authentication.Request;
    internal ApplicationAssertionReceipt Stored => stored;

    /// <summary>Loads this transaction's private acceptance after native authentication.</summary>
    internal static ManagedAssertionReceipt? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<ManagedAssertionReceipt>(typeof(ManagedAssertionReceipt).FullName!);

    /// <summary>Requires the same native transaction, compact proof, principal, client and unexpired original evidence.</summary>
    internal bool IsCurrent(OpenIddictServerTransaction transaction) => ReferenceEquals(transaction, authentication.Transaction) &&
        ReferenceEquals(transaction.Request, request) && ReferenceEquals(authentication.Request, request) &&
        authentication.EndpointType == OpenIddictServerEndpointType.Token && proof.IsCurrent(authentication) &&
        stored.Allows(authentication.Options.TimeProvider, stored.ObservedAt);
}
