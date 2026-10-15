using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Database;

/// <summary>Binds an immutable selected provider to context options and isolates EF service/model caches.</summary>
/// <example><code>options.UseDatabaseProvider(provider, connection, DatabaseStore.Platform);</code></example>
public static class ProviderOptions
{
    private const string Missing = "A registered database provider is required.";
    private const string WrongStore = "Database options do not match the context's logical store.";

    /// <summary>Rejects reversed logical-store configuration before a context can initialize or use storage.</summary>
    /// <example><code>ProviderOptions.RequireStore(options, DatabaseStore.Platform);</code></example>
    public static DbContextOptions<TContext> RequireStore<TContext>(DbContextOptions<TContext> options, DatabaseStore expected)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        var selection = options.FindExtension<SelectedProviderExtension>() ?? throw new InvalidOperationException(Missing);
        selection.Validate(options);
        if (selection.Store != expected)
        {
            throw new InvalidOperationException(WrongStore);
        }
        return options;
    }

    /// <summary>Configures and validates the selected driver without loading migrations or opening connections.</summary>
    /// <example><code>options.UseDatabaseProvider(provider, configuredConnection, DatabaseStore.Sessions);</code></example>
    public static DbContextOptionsBuilder UseDatabaseProvider(this DbContextOptionsBuilder options,
        IRelationalDatabaseProvider provider, string connection, DatabaseStore store)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);
        provider.Connections.Validate(connection);
        provider.Configure(options, connection, store);
        var extension = new SelectedProviderExtension(provider, store);
        ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(extension);
        options.ReplaceService<IModelCacheKeyFactory, ProviderModelCacheKeyFactory>();
        extension.Validate(options.Options);
        return options;
    }

    /// <summary>Returns the selected provider and rejects absent or mismatched context/driver configuration.</summary>
    /// <example><code>var provider = context.GetDatabaseProvider();</code></example>
    public static IRelationalDatabaseProvider GetDatabaseProvider(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var selection = Selection(context);
        selection.Validate(context.GetService<IDbContextOptions>());
        selection.Provider.Connections.RequireCompatible(context.Database.GetDbConnection());
        return selection.Provider;
    }

    /// <summary>Reads only the immutable selection; model construction must not recursively initialize EF services.</summary>
    /// <example><code>var dialect = ProviderOptions.Provider(options).Model;</code></example>
    public static IRelationalDatabaseProvider Provider(DbContextOptions options) =>
        options.FindExtension<SelectedProviderExtension>()?.Provider ?? throw new InvalidOperationException(Missing);

    /// <summary>Retrieves selection from EF's initialized options for cache and runtime operations.</summary>
    internal static SelectedProviderExtension Selection(DbContext context) =>
        context.GetService<IDbContextOptions>().FindExtension<SelectedProviderExtension>() ?? throw new InvalidOperationException(Missing);
}

/// <summary>Keeps service-provider reuse local to one exact provider instance and logical store.</summary>
internal sealed class SelectedProviderExtension(IRelationalDatabaseProvider provider, DatabaseStore store) : IDbContextOptionsExtension
{
    private const string Mismatch = "Database provider configuration does not match the selected registration.";
    internal IRelationalDatabaseProvider Provider
    {
        get;
    } = provider;
    internal DatabaseStore Store
    {
        get;
    } = store;
    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    /// <summary>The extension carries metadata only; driver configuration supplies actual EF services.</summary>
    public void ApplyServices(IServiceCollection services)
    {
    }

    /// <summary>Rejects multiple drivers or a driver whose assembly is not the declared EF provider.</summary>
    public void Validate(IDbContextOptions options)
    {
        var drivers = options.Extensions.Where(extension => extension.Info.IsDatabaseProvider).ToArray();
        if (drivers.Length != 1 || drivers[0].GetType().Assembly.GetName().Name != Provider.EfProviderName)
        {
            throw new InvalidOperationException(Mismatch);
        }
        if (RelationalOptionsExtension.Extract(options).Connection is { } connection)
        {
            Provider.Connections.RequireCompatible(connection);
        }
        _ = DatabaseStores.Schema(Store);
    }

    /// <summary>Uses object identity rather than user-overridable equality or connection material.</summary>
    private sealed class ExtensionInfo(SelectedProviderExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private const string SafeFragment = "RegisteredDatabaseProvider ";
        private const string DebugKey = "IdentityPlatform:DatabaseProvider";
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => SafeFragment;

        /// <summary>Isolates service caches between different trusted registrations and logical stores.</summary>
        public override int GetServiceProviderHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(extension.Provider), extension.Store);

        /// <summary>Records only an opaque cache discriminator, never configuration or connection values.</summary>
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo[DebugKey] = GetServiceProviderHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Requires the exact registration instance and store before sharing internal EF services.</summary>
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo info && ReferenceEquals(extension.Provider, ((SelectedProviderExtension)info.Extension).Provider) &&
            extension.Store == ((SelectedProviderExtension)info.Extension).Store;
    }
}
