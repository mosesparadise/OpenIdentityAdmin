using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Delays genuine native context activation before any transaction exists, isolating the original ten-second request timer.</summary>
internal sealed class GrantRequestDeadline : DbTransactionInterceptor
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(15);
    private DbContext? context;
    private int transactions;
    private bool cancelled;
    internal bool Armed
    {
        get; set;
    }

    /// <summary>Decorates the existing scoped native resolver and retains its real context and provider registration.</summary>
    internal void Configure(IServiceCollection services)
    {
        var descriptor = services.Last(row => row.ServiceType == typeof(IOpenIddictEntityFrameworkCoreContext));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        services.Replace(ServiceDescriptor.Scoped<IOpenIddictEntityFrameworkCoreContext>(provider =>
        {
            var native = descriptor.ImplementationFactory is { } factory ? factory(provider) :
                ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
            Assert.False(native is IDisposable or IAsyncDisposable);
            return new DelayedContext((IOpenIddictEntityFrameworkCoreContext)native, provider, this);
        }));
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));
    }

    /// <summary>Holds only one actual context activation until its forwarded product token expires; no transaction or cleanup timer is active here.</summary>
    private async Task HoldAsync(DbContext database, CancellationToken token)
    {
        if (!Armed)
        {
            return;
        }
        Armed = false;
        context = database;
        Assert.Null(database.Database.CurrentTransaction);
        Assert.True(token.CanBeCanceled);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token).WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
            cancelled = true;
            throw;
        }
        throw new InvalidOperationException();
    }

    /// <summary>Counts native transaction acquisition for the captured context instead of inferring absence from final database state.</summary>
    public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData data,
        InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
    {
        if (context is not null && ReferenceEquals(context, data.Context))
        {
            transactions++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Requires request cancellation without transaction acquisition and actual disposal of the native context before return.</summary>
    internal void Require()
    {
        Assert.True(cancelled);
        Assert.Equal(0, transactions);
        Assert.NotNull(context);
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = context.Model));
    }

    /// <summary>Forwards the unchanged cancellation token and returns only the real native resolver's scoped provider context.</summary>
    private sealed class DelayedContext(IOpenIddictEntityFrameworkCoreContext native, IServiceProvider services, GrantRequestDeadline probe)
        : IOpenIddictEntityFrameworkCoreContext
    {
        /// <summary>Materializes the genuine context before the optional one-shot activation delay; no provider or authorization result is replaced.</summary>
        public async ValueTask<DbContext> GetDbContextAsync(CancellationToken cancellationToken)
        {
            var database = await native.GetDbContextAsync(cancellationToken);
            Assert.Same(services.GetRequiredService<PlatformDbContext>(), database);
            await probe.HoldAsync(database, cancellationToken);
            return database;
        }
    }
}
