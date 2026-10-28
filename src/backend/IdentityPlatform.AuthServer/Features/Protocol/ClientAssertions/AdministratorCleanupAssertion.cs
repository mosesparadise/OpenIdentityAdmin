using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Explicitly composed deny-only consumption after native client authentication; production composition does not install this handler.</summary>
internal sealed class AdministratorCleanupAssertion(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    /// <summary>Consumes only one native-validated proof and never manufactures an issuance receipt or target authorization.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (!Applies(context))
        {
            return;
        }
        var proof = context.Transaction.GetProperty<ClientAssertionProof>(typeof(ClientAssertionProof).FullName!);
        var key = typeof(DurableCleanupClientAssertion).FullName!;
        if (proof is null || !proof.Claim(context) || context.Transaction.GetProperty<DurableCleanupClientAssertion>(key) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        var receipt = await DurableCleanupClientAssertion.ConsumeAsync(factory, policy, context, proof);
        if (receipt is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
        }
        else
        {
            context.Transaction.SetProperty(key, receipt);
        }
    }

    /// <summary>Limits the separately installed proof consumer to the fixed client's revocation authentication demand.</summary>
    internal static bool Applies(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        context.ClientId == AdministratorProtocolIdentity.ClientId && context.EndpointType == OpenIddictServerEndpointType.Revocation;
}

/// <summary>Applies the existing bounded proof guard only when deny-only cleanup composition is explicitly selected.</summary>
internal sealed class AdministratorCleanupAssertionGuard : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    /// <summary>Requires the native fixed-client method and proof size before the cryptographic parser is entered.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (AdministratorCleanupAssertion.Applies(context) && !AdministratorAssertionGuard.HasBoundedProof(context))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
        }
        return ValueTask.CompletedTask;
    }
}
