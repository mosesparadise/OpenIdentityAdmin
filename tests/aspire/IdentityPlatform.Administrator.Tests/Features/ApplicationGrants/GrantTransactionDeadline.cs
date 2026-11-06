using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Waits on the actual transaction token after a native save, isolating its elapsed deadline from request and caller cancellation.</summary>
internal sealed class GrantTransactionDeadline : SaveChangesInterceptor
{
    private const string Revoked = "revoked";
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(8);
    private DbContext? context;
    internal bool Armed
    {
        get; set;
    }
    internal bool Cancelled
    {
        get; private set;
    }

    /// <summary>Consumes the gate once and observes token states before cleanup can cancel the enclosing request.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
    {
        if (!Armed || data.Context is not { } database ||
            !database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Any(row => row.Entity.Status == Revoked))
        {
            return result;
        }
        Armed = false;
        context = database;
        Assert.NotNull(database.Database.CurrentTransaction);
        var request = ProtocolStorageAmbient.Current!.Scope.Owner.RequestToken;
        Assert.False(request.IsCancellationRequested);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Assert.False(request.IsCancellationRequested);
            Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
            Cancelled = true;
            throw;
        }
        throw new InvalidOperationException();
    }

    /// <summary>Requires real phase cancellation and completed native context disposal before accepting durable stop observations.</summary>
    internal void Require()
    {
        Assert.True(Cancelled);
        Assert.NotNull(context);
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = context.Model));
    }
}
