using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IdentityPlatform.Database;

/// <summary>Isolates models by context, exact registration, logical store and design-time mode.</summary>
internal sealed class ProviderModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <summary>Returns a key whose registration equality cannot be overridden by provider implementations.</summary>
    public object Create(DbContext context, bool designTime)
    {
        var selection = ProviderOptions.Selection(context);
        return new ModelKey(context.GetType(), new RegistrationIdentity(selection.Provider), selection.Store, designTime);
    }

    private sealed record ModelKey(Type Context, RegistrationIdentity Registration, DatabaseStore Store, bool DesignTime);

    /// <summary>Uses reference equality even for providers implemented as records.</summary>
    private sealed class RegistrationIdentity(IRelationalDatabaseProvider provider)
    {
        /// <summary>Shares a model only when both wrappers refer to the same registration.</summary>
        public override bool Equals(object? obj) => obj is RegistrationIdentity identity && ReferenceEquals(provider, identity.Provider);

        /// <summary>Provides a stable in-process opaque hash without connection or configuration values.</summary>
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(provider);

        private IRelationalDatabaseProvider Provider => provider;
    }
}
