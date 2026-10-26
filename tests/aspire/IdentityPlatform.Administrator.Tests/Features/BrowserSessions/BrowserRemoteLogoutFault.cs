using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Targets one exact local, remote-pending or return audit without faulting another phase.</summary>
internal sealed class BrowserRemoteLogoutFault(BrowserMutation operation) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private const string AcknowledgementFailure = "Synthetic remote logout acknowledgement loss.";

    internal Func<Task>? BeforeSave
    {
        get; set;
    }
    internal Exception? Failure
    {
        get; private set;
    }

    internal Action? AfterSave
    {
        get; set;
    }
    internal Action? AfterCleanup
    {
        get; set;
    }
    internal bool LoseAcknowledgement
    {
        get; set;
    }
    internal int Saves
    {
        get; private set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Cleanups
    {
        get; private set;
    }
    private AdminSessionDbContext? saved;

    /// <summary>Registers cleanup before the actual context so reverse scope disposal releases the context before the probe runs.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<BrowserLoginDisposalCleanup>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) =>
        {
            var cleanup = provider.GetRequiredService<BrowserLoginDisposalCleanup>();
            options.AddInterceptors(this, new CleanupObservation(this, cleanup));
        });
    }

    /// <summary>Changes the actual audit privilege only for the selected phase, before native persistence attempts its INSERT.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AdminSessionDbContext database && IsLogout(database) && BeforeSave is { } action)
        {
            await action();
        }
        return result;
    }

    /// <summary>Retains the actual provider failure privately so the test can assert its finite permission-denied classification.</summary>
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Failure = eventData.Exception;
        return Task.CompletedTask;
    }

    /// <summary>Records only a save containing the selected real browser-operation audit.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AdminSessionDbContext database && IsLogout(database))
        {
            saved = database;
            Saves++;
            AfterSave?.Invoke();
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts a genuine committed logout and optionally loses its acknowledgement after the provider has applied it.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(eventData.Context, saved))
        {
            Commits++;
            if (LoseAcknowledgement)
            {
                throw new InvalidOperationException(AcknowledgementFailure);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Recognizes the exact mutation instead of accidentally faulting local logout or another remote phase.</summary>
    private bool IsLogout(AdminSessionDbContext database) =>
        database.ChangeTracker.Entries<BrowserSessionAudit>().Any(entry => entry.Entity.Operation == operation);

    /// <summary>Runs once after disposal of the saved actual context; it never resolves a disposed scoped service.</summary>
    private void Complete(AdminSessionDbContext database)
    {
        Assert.Same(saved, database);
        Assert.Throws<ObjectDisposedException>(() => _ = database.Model);
        Cleanups++;
        AfterCleanup?.Invoke();
    }

    /// <summary>Binds disposal observation to the exact context that actually saved this scoped logout.</summary>
    private sealed class CleanupObservation(BrowserRemoteLogoutFault owner, BrowserLoginDisposalCleanup cleanup) : SaveChangesInterceptor
    {
        /// <summary>Arms cleanup only after the real logout save; unrelated cookie-validation scopes remain untouched.</summary>
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is AdminSessionDbContext database && owner.IsLogout(database))
            {
                cleanup.Complete = () => owner.Complete(database);
            }
            return ValueTask.FromResult(result);
        }
    }
}
