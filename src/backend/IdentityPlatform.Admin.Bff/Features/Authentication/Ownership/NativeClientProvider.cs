using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Substitutes only the scope factory seen by the explicitly constructed native client singleton.</summary>
internal sealed class NativeClientProvider(IServiceProvider native) : IServiceProvider
{
    private readonly IServiceScopeFactory scopes = new ClientScopes(native.GetRequiredService<IServiceScopeFactory>());

    /// <summary>Delegates options and all ordinary singleton resolution to the unmodified application provider.</summary>
    public object? GetService(Type serviceType) => serviceType == typeof(IServiceScopeFactory) ? scopes : native.GetService(serviceType);

    /// <summary>Admits native-created scopes only under an explicit live request capability.</summary>
    private sealed class ClientScopes(IServiceScopeFactory native) : IServiceScopeFactory
    {
        /// <summary>Preserves real DI scoping while deferring native context acquisition to the async factory.</summary>
        public IServiceScope CreateScope()
        {
            var parent = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
            var child = new NativeClientScope(native, parent);
            child.Enter();
            return child;
        }
    }
}
