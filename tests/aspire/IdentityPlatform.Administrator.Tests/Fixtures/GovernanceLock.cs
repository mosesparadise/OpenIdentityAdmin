using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Holds a test-owned database lock while observing real bootstrap contention.</summary>
/// <example><code>await using var heldLock = await GovernanceLock.AcquireAsync(lease);</code></example>
public sealed class GovernanceLock : IAsyncDisposable
{
    private const string SqlAuditLock = "SELECT COUNT(*) FROM [platform].[SecurityAuditIntent] WITH (TABLOCKX, HOLDLOCK)";
    private const string PostgreSqlAuditLock = "LOCK TABLE platform.\"SecurityAuditIntent\" IN ACCESS EXCLUSIVE MODE";
    private const string SqlSession = "SELECT @@SPID";
    private const string PostgreSqlSession = "SELECT pg_backend_pid()";
    private int disposed;
    private readonly AsyncServiceScope scope;
    private readonly IDbContextTransaction transaction;
    private readonly int blocker;
    private readonly bool audit;
    /// <summary>Retains the exact test-owned native blocker and its independent administrator connection.</summary>
    private GovernanceLock(AsyncServiceScope scope, IDbContextTransaction transaction, int blocker, bool audit)
    {
        this.scope = scope;
        this.transaction = transaction;
        this.blocker = blocker;
        this.audit = audit;
    }

    /// <summary>Acquires either the governance singleton or audit table on the disposable lease for native wait-graph evidence.</summary>
    /// <example><code>await using var heldLock = await GovernanceLock.AcquireAsync(lease, audit: true);</code></example>
    public static async Task<GovernanceLock> AcquireAsync(BootstrapDatabase lease, bool audit = false)
    {
        var scope = lease.Administrator.CreateAsyncScope();
        try
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            try
            {
                if (audit)
                {
                    var sql = database.Database.IsSqlServer()
                        ? SqlAuditLock : PostgreSqlAuditLock;
                    await database.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
                }
                else
                {
                    await database.Set<AdministratorGovernance>().ExecuteUpdateAsync(update => update.SetProperty(value => value.Revision, value => value.Revision + 1), TestContext.Current.CancellationToken);
                }
                await using var identity = database.Database.GetDbConnection().CreateCommand();
                identity.Transaction = transaction.GetDbTransaction();
                identity.CommandText = database.Database.IsSqlServer() ? SqlSession : PostgreSqlSession;
                var blocker = Convert.ToInt32(await identity.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                return new GovernanceLock(scope, transaction, blocker, audit);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    /// <summary>Observes native blocked statements instead of inferring contention from elapsed time.</summary>
    /// <example><code>await heldLock.ObserveWaitersAsync(lease, minimum: 1);</code></example>
    public Task ObserveWaitersAsync(BootstrapDatabase lease, int minimum)
    {
        const string AuditStage = "%INSERT%SecurityAuditIntent%";
        const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
        return DatabaseWaiters.ObserveAsync(lease, blocker, audit ? AuditStage : GovernanceStage, minimum);
    }

    /// <summary>Terminates only a runtime session independently observed executing the expected blocked audit INSERT.</summary>
    internal Task TerminateAuditWaiterAsync(BootstrapDatabase lease)
    {
        Assert.True(audit);
        return DatabaseWaiters.TerminateAuditWaiterAsync(lease, blocker);
    }

    /// <summary>Releases each owner once, including early failed-test cleanup before normal asynchronous disposal.</summary>
    /// <example><code>await heldLock.DisposeAsync(); // Allows a failed subject to drain before its context is released.</code></example>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            await scope.DisposeAsync();
        }
    }
    /// <summary>Allows the test owner to cancel and join the exact native wait-graph probe after subject completion.</summary>
    internal Task ObserveOwnedAsync(BootstrapDatabase lease, int minimum, CancellationToken token)
    {
        const string AuditStage = "%INSERT%SecurityAuditIntent%";
        const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
        return DatabaseWaiters.ObserveOwnedAsync(lease, blocker, audit ? AuditStage : GovernanceStage, minimum, token);
    }
}
