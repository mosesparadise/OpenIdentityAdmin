using Microsoft.AspNetCore.Authentication;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol;

public static class ProtocolError
{
    public static IResult Create(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The request could not be completed."
    }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
