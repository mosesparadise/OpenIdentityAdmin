using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.Logging.Abstractions;
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
    ProtocolStorageOwner owner, NativeClientProfile? profile = null) : IOpenIddictClientFactory
{
    /// <summary>Preserves the native transaction/options and suppresses credential-bearing native diagnostics only for the fixed client profile.</summary>
    public async ValueTask<OpenIddictClientTransaction> CreateTransactionAsync()
    {
        if (activation.Scope is not null)
        {
            await activation.Scope.ActivateAsync();
        }
        owner.RequireHealthy();
        var transaction = await native.CreateTransactionAsync();
        if (profile is not null)
        {
            transaction.Logger = NullLogger.Instance;
        }
        return transaction;
    }
}
