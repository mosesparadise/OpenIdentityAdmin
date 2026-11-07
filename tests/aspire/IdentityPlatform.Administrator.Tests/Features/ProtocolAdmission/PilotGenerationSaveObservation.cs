using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Binds Added access state, actual provider INSERT and completed native save before a generation fault may count.</summary>
internal sealed class PilotGenerationSaveObservation(PilotGenerationFault fault, PilotGenerationCleanup cleanup)
    : SaveChangesInterceptor, IDbCommandInterceptor
{
    private const string Insert = "INSERT";
    private const string Tokens = "OpenIddictTokens";
    private OpenIddictEntityFrameworkCoreToken? pending;

    /// <summary>Targets only a newly added native access entry; code consumption and request-cache writes remain untouched.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (fault.Armed)
        {
            var added = eventData.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>()
                .Where(entry => entry.State == EntityState.Added && entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken).ToArray();
            if (added.Length != 0)
            {
                Assert.Null(pending);
                pending = Assert.Single(added).Entity;
                fault.Begin(eventData.Context, cleanup);
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Observes RETURNING/OUTPUT-based INSERT execution without reading or modifying the native result reader.</summary>
    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Observe(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Also observes providers that execute an INSERT without a result reader.</summary>
    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Observe(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts the actual INSERT only while the exact Added access entry is being saved.</summary>
    private void Observe(DbCommand command, CommandExecutedEventData eventData)
    {
        if (pending is not null && command.CommandText.Contains(Insert, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            fault.Inserted(eventData.Context!, command.Transaction);
        }
    }

    /// <summary>Requires successful row persistence and native state acceptance before observing committed consumption or failing.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (pending is not null)
        {
            Assert.True(result > 0);
            Assert.Equal(EntityState.Unchanged, eventData.Context!.Entry(pending).State);
            var identifier = pending.Id;
            pending = null;
            await fault.SavedAsync(eventData.Context, identifier!);
        }
        return result;
    }
}
