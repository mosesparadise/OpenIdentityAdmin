using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Links only the native service-created child to its deferred activation boundary.</summary>
internal sealed class NativeClientActivation
{
    internal NativeClientScope? Scope
    {
        get; set;
    }
}

/// <summary>Activates a service-created child before forwarding to the installed native factory.</summary>
internal sealed class OwnedClientFactory(OpenIddictClientFactory native, NativeClientActivation activation,
    ProtocolStorageOwner owner) : IOpenIddictClientFactory
{
    /// <summary>Preserves the native transaction/options/logger while requiring an active original owner.</summary>
    public async ValueTask<OpenIddictClientTransaction> CreateTransactionAsync()
    {
        if (activation.Scope is not null)
        {
            await activation.Scope.ActivateAsync();
        }
        owner.RequireHealthy();
        return await native.CreateTransactionAsync();
    }
}
