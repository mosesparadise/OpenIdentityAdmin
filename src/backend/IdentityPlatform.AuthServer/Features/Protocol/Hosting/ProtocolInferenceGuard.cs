using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Rejects every configured endpoint that did not enter its declared host transport before extraction can resolve managers.</summary>
internal sealed class ProtocolInferenceGuard(ProtocolHostRequest request) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessRequestContext>
{
    /// <summary>Checks the actual inferred endpoint against the immutable middleware decision.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        request.RequireInferred(context);
        return AdministratorTransportGuard.ValidateAsync(context, request);
    }
}
