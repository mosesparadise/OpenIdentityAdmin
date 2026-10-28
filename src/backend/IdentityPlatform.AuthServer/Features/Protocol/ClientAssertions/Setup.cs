using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Offers dormant fixed-client assertion integration at pinned native validation and grant boundaries.</summary>
internal static class Setup
{
    private const int BetweenNativeHandlers = 500;
    internal static int CleanupConsumptionOrder => OpenIddictServerHandlers.ValidateClientCertificate.Descriptor.Order + BetweenNativeHandlers;

    /// <summary>Adds deny-only proof consumption to an existing assertion pipeline; it neither repeats base composition nor creates endpoints or permissions.</summary>
    internal static OpenIddictServerBuilder AddAdministratorCleanupAssertions(this OpenIddictServerBuilder builder)
    {
        builder.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler.UseScopedHandler<AdministratorCleanupAssertionGuard>()
            .SetOrder(OpenIddictServerHandlers.ValidateClientAssertion.Descriptor.Order - BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler.UseScopedHandler<AdministratorCleanupAssertion>()
            .SetOrder(CleanupConsumptionOrder));
        return builder;
    }

    /// <summary>Bounds before parsing, captures after validation, and independently consumes after native client authentication but before grant validation.</summary>
    internal static OpenIddictServerBuilder AddAdministratorAssertions(this OpenIddictServerBuilder builder)
    {
        builder.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler.UseScopedHandler<AdministratorAssertionGuard>()
            .SetOrder(OpenIddictServerHandlers.ValidateClientAssertion.Descriptor.Order - BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(handler => handler.UseScopedHandler<AdministratorAssertionCapture>()
            .SetOrder(OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry.Descriptor.Order + BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler.UseScopedHandler<AdministratorClientAssertion>()
            .SetOrder(OpenIddictServerHandlers.ValidateClientCertificate.Descriptor.Order + BetweenNativeHandlers));
        return builder;
    }
}
