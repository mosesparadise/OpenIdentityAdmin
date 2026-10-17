using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Creates genuinely distinct unopened contexts and records their actual disposal across a shared request.</summary>
internal sealed class StorageRequestProbe : IAsyncDisposable
{
    private const string UnopenedConnection = "Host=localhost;Database=unused_request_probe";
    internal StorageProbe.ProbeClock Clock { get; } = new();
    internal List<StorageProbe.ProbeContext> Databases { get; } = [];
    internal ServiceProvider Root
    {
        get;
    }
    internal IServiceScopeFactory Factory => Root.GetRequiredService<IServiceScopeFactory>();

    /// <summary>Allows native dispatcher registrations while preserving actual DI child-scope ownership.</summary>
    internal StorageRequestProbe(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped(_ =>
        {
            var database = new StorageProbe.ProbeContext(new DbContextOptionsBuilder().UseNpgsql(UnopenedConnection).Options);
            Databases.Add(database);
            return database;
        });
        services.AddScoped<IOpenIddictEntityFrameworkCoreContext, NativeContext>();
        services.AddScoped<ProtocolStorageOwner>();
        configure?.Invoke(services);
        Root = services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Disposes the root only after every enclosing request operation has ended.</summary>
    public ValueTask DisposeAsync() => Root.DisposeAsync();

    /// <summary>Returns the context resolved from the same native child provider.</summary>
    private sealed class NativeContext(StorageProbe.ProbeContext database) : IOpenIddictEntityFrameworkCoreContext
    {
        /// <summary>Preserves scoped reference identity without a database connection.</summary>
        public ValueTask<DbContext> GetDbContextAsync(CancellationToken cancellationToken) => new(database);
    }
}
