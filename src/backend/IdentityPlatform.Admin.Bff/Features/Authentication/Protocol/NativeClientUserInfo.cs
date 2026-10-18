using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Disables native optional UserInfo retrieval for the fixed no-profile administrator flow.</summary>
internal sealed class NativeClientUserInfo : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Runs before native evaluation; code-flow completion never authorizes a separate bearer-bearing UserInfo call.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        context.DisableUserInfoRetrieval = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Rejects a later attempt to re-enable optional UserInfo transport before its native send handler.</summary>
internal sealed class NativeClientUserInfoFence : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>No fixed-profile path may reach a UserInfo send, regardless of late mutable context changes.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (context.SendUserInfoRequest || !context.DisableUserInfoRetrieval)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return ValueTask.CompletedTask;
    }
}
