using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Provides an unopened relational context and independently controlled monotonic time for ownership tests.</summary>
internal sealed class StorageProbe : IAsyncDisposable
{
    private const string UnopenedConnection = "Host=localhost;Database=unused_ownership_probe";
    internal ProbeClock Clock { get; } = new();
    internal ProbeContext Database { get; } = new(new DbContextOptionsBuilder().UseNpgsql(UnopenedConnection).Options);
    internal int Resolutions
    {
        get; private set;
    }
    internal ServiceProvider Root
    {
        get;
    }

    /// <summary>Registers a real scoped owner while ensuring its context is disposed only by the owned child scope.</summary>
    internal StorageProbe()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped(_ => { Resolutions++; return Database; });
        services.AddScoped<IOpenIddictEntityFrameworkCoreContext, ProbeNative>();
        services.AddScoped<ProtocolStorageOwner>();
        Root = services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Starts the actual ownership handle without opening a database connection.</summary>
    internal Task<ProtocolStorageScope> OpenAsync(CancellationToken token = default) =>
        ProtocolStorageScope.OpenAsync(Root.GetRequiredService<IServiceScopeFactory>(), token);

    /// <summary>Disposes the fixture root after its owning handles have completed.</summary>
    public ValueTask DisposeAsync() => Root.DisposeAsync();

    /// <summary>Allows elapsed-time checks to be tested independently of timer callback scheduling.</summary>
    internal sealed class ProbeClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <summary>Returns the controlled monotonic timestamp.</summary>
        public override long GetTimestamp() => ticks;
        /// <summary>Advances elapsed time while leaving real timers alone to expose completed-task expiry checks.</summary>
        internal void Advance(TimeSpan elapsed) => ticks += elapsed.Ticks;
    }

    /// <summary>Counts actual context disposal so a caller timeout cannot masquerade as safe cleanup.</summary>
    internal sealed class ProbeContext(DbContextOptions options) : DbContext(options)
    {
        internal int Disposals
        {
            get; private set;
        }
        /// <summary>Records disposal of the native context itself.</summary>
        public override async ValueTask DisposeAsync()
        {
            Disposals++;
            await base.DisposeAsync();
        }
    }

    /// <summary>Retains native scoped-context lookup behavior without opening a connection.</summary>
    private sealed class ProbeNative(ProbeContext database) : IOpenIddictEntityFrameworkCoreContext
    {
        /// <summary>Returns the identical wrapped context for the lifetime of the child scope.</summary>
        public ValueTask<DbContext> GetDbContextAsync(CancellationToken cancellationToken) => new(database);
    }
}
