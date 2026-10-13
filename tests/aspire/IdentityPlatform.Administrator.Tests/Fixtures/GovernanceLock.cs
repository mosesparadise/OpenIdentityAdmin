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
    private readonly AsyncServiceScope scope;
    private readonly IDbContextTransaction transaction;
    private readonly int blocker;
    private readonly bool audit;
    private GovernanceLock(AsyncServiceScope scope, IDbContextTransaction transaction, int blocker, bool audit)
    {
        this.scope = scope;
        this.transaction = transaction;
        this.blocker = blocker;
        this.audit = audit;
    }

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
                        ? "SELECT COUNT(*) FROM [platform].[SecurityAuditIntent] WITH (TABLOCKX, HOLDLOCK)"
                        : "LOCK TABLE platform.\"SecurityAuditIntent\" IN ACCESS EXCLUSIVE MODE";
                    await database.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
                }
                else
                {
                    await database.Set<AdministratorGovernance>().ExecuteUpdateAsync(update => update.SetProperty(value => value.Revision, value => value.Revision + 1), TestContext.Current.CancellationToken);
                }
                await using var identity = database.Database.GetDbConnection().CreateCommand();
                identity.Transaction = transaction.GetDbTransaction();
                identity.CommandText = database.Database.IsSqlServer() ? "SELECT @@SPID" : "SELECT pg_backend_pid()";
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

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            await scope.DisposeAsync();
        }
    }
}
