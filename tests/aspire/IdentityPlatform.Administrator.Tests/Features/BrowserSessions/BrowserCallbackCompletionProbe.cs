using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Observes actual establishment commit or exact context disposal without replacing any provider operation.</summary>
internal sealed class BrowserCallbackCompletionProbe(string fault, Action expire)
{
    private const string Commit = "commit";
    private const string LocalDisposal = "local-disposal";
    private const string NativeDisposal = "native-disposal";
    private const string SourceExpiry = "source-expiry";
    private const string Expiry = "expiry";
    private const string CommitFailure = "Synthetic callback commit acknowledgement loss.";
    private const string CleanupFailure = "Synthetic callback cleanup failure.";

    private const int ObservationOffset = 1000;
    internal int Calls
    {
        get; private set;
    }

    /// <summary>Constructs cleanup before each database and targets only final local ticket state or completed native callback roots.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<BrowserLoginDisposalCleanup>();
        services.AddScoped(provider => new BrowserCallbackCommitProbe(this, provider.GetRequiredService<BrowserLoginDisposalCleanup>()));
        services.AddDbContext<AdminSessionDbContext>((provider, options) =>
        {
            options.AddInterceptors(provider.GetRequiredService<BrowserCallbackCommitProbe>());
        });
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler
            .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order + ObservationOffset).UseInlineHandler(_ =>
            {
                if (fault is NativeDisposal or Expiry or SourceExpiry)
                {
                    var scope = ProtocolStorageAmbient.Current!.Scope;
                    var database = (AdminSessionDbContext)scope.Owner.Database;
                    scope.Services.GetRequiredService<BrowserLoginDisposalCleanup>().Complete = () => Dispose(database);
                }
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Only an actual committed T005 ticket transaction can trigger acknowledgement loss or its local disposal hook.</summary>
    internal void Committed(TransactionEndEventData eventData, BrowserLoginDisposalCleanup cleanup)
    {
        if (eventData.Context is AdminSessionDbContext database && database.ChangeTracker.Entries<AdminSessionTicket>().Any())
        {
            if (fault == Commit)
            {
                Calls++;
                throw new InvalidOperationException(CommitFailure);
            }
            if (fault == LocalDisposal)
            {
                cleanup.Complete = () => Dispose(database);
            }
        }
    }

    /// <summary>Requires the observed context to be really disposed before failing cleanup or crossing the original pending deadline.</summary>
    private void Dispose(AdminSessionDbContext database)
    {
        Assert.Throws<ObjectDisposedException>(() => _ = database.Model);
        Calls++;
        if (fault is Expiry or SourceExpiry)
        {
            expire();
        }
        else
        {
            throw new InvalidOperationException(CleanupFailure);
        }
    }
}

/// <summary>Retains the exact local scope's cleanup hook while observing a real provider commit.</summary>
internal sealed class BrowserCallbackCommitProbe(BrowserCallbackCompletionProbe probe, BrowserLoginDisposalCleanup cleanup) : DbTransactionInterceptor
{
    /// <summary>Delegates only after the database has actually acknowledged its commit.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        probe.Committed(eventData, cleanup);
        return Task.CompletedTask;
    }
}
