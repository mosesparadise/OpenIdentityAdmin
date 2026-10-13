using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Owns an administrator-only native table blocker for actual manager cancellation controls.</summary>
internal sealed class GovernanceNativeBlock : IAsyncDisposable
{
    private const string SqlRoleLock = "SELECT COUNT(*) FROM [platform].[AspNetRoles] WITH (TABLOCKX, HOLDLOCK)";
    private const string SqlUserLock = "SELECT COUNT(*) FROM [platform].[AspNetUsers] WITH (TABLOCKX, HOLDLOCK)";
    private const string PostgreSqlRoleLock = "LOCK TABLE platform.\"AspNetRoles\" IN ACCESS EXCLUSIVE MODE";
    private const string PostgreSqlUserLock = "LOCK TABLE platform.\"AspNetUsers\" IN ACCESS EXCLUSIVE MODE";
    private readonly AsyncServiceScope scope;
    private readonly IDbContextTransaction transaction;
    private readonly int blocker;
    private readonly bool role;
    private int disposed;

    /// <summary>Retains native transaction ownership and a bounded provider session identifier.</summary>
    private GovernanceNativeBlock(AsyncServiceScope scope, IDbContextTransaction transaction, int blocker, bool role)
    {
        this.scope = scope;
        this.transaction = transaction;
        this.blocker = blocker;
        this.role = role;
    }

    /// <summary>Acquires only one of two closed test table targets before the subject opens its native transaction.</summary>
    internal static async Task<GovernanceNativeBlock> AcquireAsync(BootstrapDatabase lease, bool role)
    {
        var scope = lease.Administrator.CreateAsyncScope();
        IDbContextTransaction? transaction = null;
        try
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var token = TestContext.Current.CancellationToken;
            transaction = await database.Database.BeginTransactionAsync(token);
            await database.Database.ExecuteSqlRawAsync(Statement(database.Database.IsSqlServer(), role), token);
            return new GovernanceNativeBlock(scope, transaction, await DatabaseWaiters.IdentifyAsync(database, token), role);
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
            finally { await scope.DisposeAsync(); }
            throw;
        }
    }

    /// <summary>Observes only the actual blocked native manager statement in the isolated leased database.</summary>
    internal Task ObserveAsync(BootstrapDatabase lease, CancellationToken token)
    {
        const string RoleStatement = "%AspNetRoles%";
        const string UserStatement = "%AspNetUsers%";
        return DatabaseWaiters.ObserveOwnedAsync(lease, blocker, role ? RoleStatement : UserStatement, 1, token);
    }

    /// <summary>Uses fixed SQL with no external identifier interpolation or suppressed native validation.</summary>
    private static string Statement(bool sql, bool role) => (sql, role) switch
    {
        (true, true) => SqlRoleLock,
        (true, false) => SqlUserLock,
        (false, true) => PostgreSqlRoleLock,
        (false, false) => PostgreSqlUserLock
    };

    /// <summary>Releases the native lock before disposing its independently owned administrator connection.</summary>
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
        finally { await scope.DisposeAsync(); }
    }
}
