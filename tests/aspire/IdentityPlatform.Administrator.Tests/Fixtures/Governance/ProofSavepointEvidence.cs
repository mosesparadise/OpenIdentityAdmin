using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Configures real SQL Server MARS and observes actual savepoint calls without substituting provider capabilities or results.</summary>
internal sealed class ProofSavepointEvidence : DbTransactionInterceptor
{
    internal const string PostgreSql = "PostgreSql";
    internal const string SqlServer = "SqlServer";
    private bool armed;
    private int attempted;
    private int completed;

    /// <summary>Changes the real registered SQL provider option, retaining other configured relational options; PostgreSQL remains unchanged.</summary>
    internal void Configure(DbContextOptionsBuilder options, string provider)
    {
        if (provider == SqlServer)
        {
            var connection = RelationalOptionsExtension.Extract(options.Options).ConnectionString;
            Assert.NotNull(connection);
            var configured = new SqlConnectionStringBuilder(connection) { MultipleActiveResultSets = true };
            options.UseSqlServer(configured.ConnectionString);
        }
        else
        {
            Assert.Equal(PostgreSql, provider);
        }
        options.AddInterceptors(this);
    }

    /// <summary>Isolates the actual verification from enrollment/setup and their ordinary automatic savepoints.</summary>
    internal void Begin()
    {
        attempted = 0;
        completed = 0;
        armed = true;
    }

    /// <summary>Stops verification observations before transaction disposal and fixture cleanup.</summary>
    internal void Stop() => armed = false;

    /// <summary>Requires supported providers to create a real savepoint and unsupported providers to reject before any attempt.</summary>
    internal void Require(bool supported)
    {
        if (supported)
        {
            Assert.True(attempted > 0);
            Assert.Equal(attempted, completed);
        }
        else
        {
            Assert.Equal(0, attempted);
            Assert.Equal(0, completed);
        }
    }

    /// <summary>Observes creation intent while retaining the untouched native interception decision.</summary>
    public override ValueTask<InterceptionResult> CreatingSavepointAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (armed)
        {
            attempted++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts completed native savepoints without retaining provider SQL, credentials or transaction identifiers.</summary>
    public override Task CreatedSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default)
    {
        if (armed)
        {
            completed++;
        }
        return Task.CompletedTask;
    }
}
