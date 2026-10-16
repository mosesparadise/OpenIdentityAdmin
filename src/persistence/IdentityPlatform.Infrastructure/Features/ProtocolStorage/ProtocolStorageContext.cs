using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Validates the exact context each time a native base method obtains it, including tracked-navigation fast paths.</summary>
internal sealed class ProtocolStorageContext(IOpenIddictEntityFrameworkCoreContext native, ProtocolStorageOwner owner)
    : IOpenIddictEntityFrameworkCoreContext
{
    /// <summary>Rejects changed context ownership before any native base-store query or mutation can execute.</summary>
    public async ValueTask<DbContext> GetDbContextAsync(CancellationToken cancellationToken)
    {
        var context = await native.GetDbContextAsync(cancellationToken);
        owner.RequireContext(context);
        return context;
    }
}
