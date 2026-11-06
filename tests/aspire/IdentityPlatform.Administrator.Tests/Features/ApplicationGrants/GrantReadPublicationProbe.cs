using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Binds the rollback-only grant query to its exact provider transaction cleanup, separately from native bearer validation.</summary>
internal sealed class GrantReadPublicationProbe : GrantCleanupProbe
{
    private const string JobTable = "ApplicationGrantJob";
    internal bool Armed
    {
        get; set;
    }
    internal int Reads
    {
        get; private set;
    }

    /// <summary>Defers selection lookup until the actual query, avoiding context construction recursion and disposable DI sentinels.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((provider, options) =>
        options.AddInterceptors(this, new ReadInterceptor(this, () => provider.GetRequiredService<ProtocolStorageSelection>())));

    /// <summary>Only a completed grant query on a real API-read scope can select the cleanup transaction.</summary>
    private sealed class ReadInterceptor(GrantReadPublicationProbe probe, Func<ProtocolStorageSelection> selection) : DbCommandInterceptor
    {
        /// <summary>Captures the exact native transaction after the status SELECT, leaving earlier authentication and unrelated query cleanup untouched.</summary>
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (probe.Armed && command.CommandText.Contains(JobTable, StringComparison.Ordinal))
            {
                selection().Require(ProtocolStorageMode.ApiRead);
                probe.Armed = false;
                probe.Reads++;
                var database = eventData.Context!;
                var transaction = database.Database.CurrentTransaction!;
                probe.Bind(database, transaction.TransactionId, transaction.GetDbTransaction());
            }
            return ValueTask.FromResult(result);
        }
    }
}
