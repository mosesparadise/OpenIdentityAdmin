using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Observes actual provider lock contention and holds only the winner's original commit under unchanged deadlines.</summary>
internal sealed class BrowserUpgradeRaceProbe(bool winner, bool nativeIssuance = false, string? lockTable = null) : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const string Update = "UPDATE";
    private const string Governance = "AdministratorGovernance";
    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource<int> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Registers only fixture observers over actual native runtime contexts.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Captures the peer's exact database session before it attempts the already-held governance UPDATE.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && !winner && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(lockTable ?? Governance, StringComparison.Ordinal))
        {
            Reached.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Holds the actual winner only after all command preconditions/writes and final checks reached its provider commit.</summary>
    public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Armed && winner && Owns(data.Context))
        {
            Assert.False(Reached.Task.IsCompleted);
            Reached.TrySetResult(NativeLockObservation.Session(transaction.Connection!));
            await Release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Excludes early durable assertion commits from the actual native PAR issuance boundary.</summary>
    private bool Owns(DbContext? context) => context is not null && (nativeIssuance ?
        context.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Any(entry => entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken) :
        context.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.Event == BrowserUpgradeFixture.AuditEvent));

    /// <summary>Counts successful native commit callbacks independently of method return order.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (Armed && Owns(data.Context))
        {
            Commits++;
        }
        return Task.CompletedTask;
    }
}
