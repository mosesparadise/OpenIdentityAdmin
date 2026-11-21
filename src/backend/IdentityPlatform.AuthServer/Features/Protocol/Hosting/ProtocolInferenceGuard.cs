using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Rejects every configured endpoint that did not enter its declared host transport before extraction can resolve managers.</summary>
internal sealed class ProtocolInferenceGuard(ProtocolHostRequest request, ProtocolProfiles? profiles = null) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessRequestContext>
{
    /// <summary>Checks the actual inferred endpoint against the immutable middleware decision.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        request.RequireInferred(context);
        if (profiles is { Managed: true } && !request.IsPilot && ManagedTokenTransport.Candidate(context))
        {
            await ManagedTokenTransport.ValidateAsync(context);
            return;
        }
        if (profiles is { Managed: true } && !request.IsPilot && await NativeTokenTransport.TryHandleAsync(context))
        {
            return;
        }
        await (profiles is { Managed: true } && !request.IsPilot && ManagedTransport.Candidate(context)
            ? ManagedTransport.ValidateAsync(context) : AdministratorTransportGuard.ValidateAsync(context, request));
    }
}
