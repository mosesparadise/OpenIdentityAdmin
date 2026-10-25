using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Targets actual logout persistence rather than the named cookie validator's earlier metadata commit.</summary>
internal sealed class IssuerLogoutFault : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private const string AcknowledgementFailure = "Synthetic issuer logout acknowledgement loss.";
    private const string AuditEvent = "administrator-logout";
    private const string Accepted = "accepted";

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
    private PlatformDbContext? saved;

    /// <summary>Registers cleanup before the actual context so reverse scope disposal releases the context before the probe runs.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<BrowserLoginDisposalCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            var cleanup = provider.GetRequiredService<BrowserLoginDisposalCleanup>();
            options.AddInterceptors(this, new CleanupObservation(this, cleanup));
        });
    }

    /// <summary>Records only a save containing the real accepted logout audit and revoked source.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is PlatformDbContext database && IsLogout(database))
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

    /// <summary>Recognizes the exact mutation instead of accidentally faulting cookie validation or unrelated native work.</summary>
    private static bool IsLogout(PlatformDbContext database) =>
        database.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.Event == AuditEvent && entry.Entity.Outcome == Accepted) &&
        database.ChangeTracker.Entries<AdministratorSession>().Any(entry => entry.Entity.Revoked);

    /// <summary>Runs once after disposal of the saved actual context; it never resolves a disposed scoped service.</summary>
    private void Complete(PlatformDbContext database)
    {
        Assert.Same(saved, database);
        Assert.Throws<ObjectDisposedException>(() => _ = database.Model);
        Cleanups++;
        AfterCleanup?.Invoke();
    }

    /// <summary>Binds disposal observation to the exact context that actually saved this scoped logout.</summary>
    private sealed class CleanupObservation(IssuerLogoutFault owner, BrowserLoginDisposalCleanup cleanup) : SaveChangesInterceptor
    {
        /// <summary>Arms cleanup only after the real logout save; unrelated cookie-validation scopes remain untouched.</summary>
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is PlatformDbContext database && IsLogout(database))
            {
                cleanup.Complete = () => owner.Complete(database);
            }
            return ValueTask.FromResult(result);
        }
    }
}
