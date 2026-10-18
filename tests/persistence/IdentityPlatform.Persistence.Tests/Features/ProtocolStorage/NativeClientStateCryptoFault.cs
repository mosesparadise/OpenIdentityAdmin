using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Mutates only actual native crypto inputs while preserving the original prepared principal and stored-row shape.</summary>
internal static class NativeClientStateCryptoFault
{
    private const string Changed = "synthetic-descriptor-change";

    /// <summary>Requires each configured attack to reach its exact pre-crypto pipeline boundary.</summary>
    internal static void Apply(OpenIddictClientEvents.GenerateTokenContext context, NativeClientStateFault fault)
    {
        switch (fault.Mode)
        {
            case NativeClientStateFault.Kind.ChangedDescriptorNonce:
                context.SecurityTokenDescriptor.Subject!.AddClaim(new Claim(OpenIddictConstants.Claims.Private.Nonce, Changed));
                break;
            case NativeClientStateFault.Kind.RemovedEncryption:
                context.EncryptionCredentials = null;
                break;
            case NativeClientStateFault.Kind.ChangedDescriptorType:
                context.SecurityTokenDescriptor.TokenType = Changed;
                break;
            case NativeClientStateFault.Kind.ChangedDescriptorClaims:
                context.SecurityTokenDescriptor.Claims![OpenIddictConstants.Claims.Private.CodeVerifier] = Changed;
                break;
            case NativeClientStateFault.Kind.ChangedHandler:
                context.SecurityTokenHandler = new();
                break;
            default:
                return;
        }
        fault.Calls++;
    }
}
