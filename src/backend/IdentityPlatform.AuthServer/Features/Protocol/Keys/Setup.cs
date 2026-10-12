using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Keys;

internal static class Setup
{
    internal static OpenIddictServerBuilder AddIssuerKeyPolicy(this OpenIddictServerBuilder options, IssuerCredentials credentials)
    {
        foreach (var certificate in credentials.SigningKeys.All)
        {
            options.AddSigningCertificate(certificate);
        }
        foreach (var certificate in credentials.EncryptionKeys.All)
        {
            options.AddEncryptionCertificate(certificate);
        }
        options.AddEventHandler<GenerateTokenContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Protection.AttachSecurityCredentials.Descriptor.Order + 500)
            .UseInlineHandler(context => ActiveKeyPolicy.ApplyAsync(context, credentials)));
        return options;
    }
}
