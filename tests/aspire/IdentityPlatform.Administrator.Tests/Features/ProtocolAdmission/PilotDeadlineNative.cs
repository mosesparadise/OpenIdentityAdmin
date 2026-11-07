using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Correlates exact native gate execution and access INSERT/save without replacing any provider command or result.</summary>
internal sealed class PilotDeadlineNative(PilotDeadlineProbe probe, PilotDeadlineCleanup cleanup) : SaveChangesInterceptor, IDbCommandInterceptor
{
    private const string Gate = "PilotProtocolGate";
    private const string Tokens = "OpenIddictTokens";
    private const string Update = "UPDATE";
    private const string Insert = "INSERT";
    private OpenIddictEntityFrameworkCoreToken? access;

    /// <summary>Recognizes the actual pending native access entry, excluding validation and early consumption writes.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (probe.Armed)
        {
            var entries = eventData.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>()
                .Where(entry => entry.State == EntityState.Added && entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken).ToArray();
            if (entries.Length != 0)
            {
                Assert.Null(access);
                access = Assert.Single(entries).Entity;
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Requires the same real gate statement and one affected singleton before binding native context ownership.</summary>
    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (probe.Armed && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Gate, StringComparison.Ordinal))
        {
            Assert.Equal(1, result);
            Assert.NotNull(command.Transaction);
            probe.Acquired(eventData.Context!, command.Transaction, cleanup);
        }
        ObserveInsert(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Observes actual RETURNING/OUTPUT provider execution without inspecting the private result values.</summary>
    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        ObserveInsert(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts INSERT only while the native tracked access entry is pending in this exact context.</summary>
    private void ObserveInsert(DbCommand command, CommandExecutedEventData eventData)
    {
        if (access is not null && command.CommandText.Contains(Insert, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            probe.Inserted(eventData.Context!, command.Transaction);
        }
    }

    /// <summary>Advances time only after provider execution and native EF state acceptance; a suppressed or unsuccessful save cannot satisfy the boundary.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (access is not null)
        {
            Assert.True(result > 0);
            Assert.Equal(EntityState.Unchanged, eventData.Context!.Entry(access).State);
            var id = access.Id;
            access = null;
            await probe.SavedAsync(eventData.Context, id!);
        }
        return result;
    }
}
