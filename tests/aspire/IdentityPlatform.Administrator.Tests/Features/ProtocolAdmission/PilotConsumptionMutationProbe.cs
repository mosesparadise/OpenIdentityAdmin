using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes actual native redemption persistence and its exact transaction outcome without changing protocol inputs.</summary>
internal sealed class PilotConsumptionMutationProbe : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private DbContext? database;
    private OpenIddictEntityFrameworkCoreToken? code;
    internal bool Armed
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
    internal int Rollbacks
    {
        get; private set;
    }
    internal string Identifier => code!.Id!;

    /// <summary>Observes only real provider saves and transaction callbacks; no manager or native handler is replaced.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Captures the exact native code while the genuine redemption write is pending.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            var changed = eventData.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Where(entry =>
                entry.State == EntityState.Modified && entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode &&
                entry.Entity.Status == OpenIddictConstants.Statuses.Redeemed).ToArray();
            if (changed.Length != 0)
            {
                Assert.Null(database);
                database = eventData.Context;
                code = Assert.Single(changed).Entity;
                Assert.NotNull(database.Database.CurrentTransaction);
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts completed provider persistence, not merely an attempted native mutation.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(eventData.Context, database))
        {
            Assert.True(result > 0);
            Assert.Equal(EntityState.Unchanged, database!.Entry(code!).State);
            Assert.NotNull(code!.RedemptionDate);
            Saves++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Records an actual provider commit for the independently captured native consumption context.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(eventData.Context, database))
        {
            Commits++;
        }
        return Task.CompletedTask;
    }

    /// <summary>Records explicit native rollback without treating another scope's outcome as consumption evidence.</summary>
    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(eventData.Context, database))
        {
            Rollbacks++;
        }
        return Task.CompletedTask;
    }
}
