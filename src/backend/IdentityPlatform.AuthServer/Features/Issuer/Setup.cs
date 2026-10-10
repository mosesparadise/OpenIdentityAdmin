using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;

namespace IdentityPlatform.AuthServer.Features.Issuer;

internal static class Setup
{
    internal static IssuerCredentials? AddIssuer(this IServiceCollection services, IConfiguration configuration) =>
        services.AddIssuerProtection(configuration);

    internal static WebApplication VerifyIssuer(this WebApplication app, IssuerCredentials? credentials)
    {
        if (credentials is not null)
        {
            app.Services.VerifyIssuerProtection();
        }
        return app;
    }
}
