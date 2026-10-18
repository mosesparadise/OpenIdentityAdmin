using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves the shared cleanup budget can make committed progress while preserving live, boundary and foreign-dependent rows.</summary>
internal static class NativeTokenCleanup
{
    private const int CleanupRows = 100;
    private const int AuthorizationLimit = 2000;
    private const int RetentionDays = 14;
    private const int GraceSeconds = 30;
    private const string ParentId = "quota-cleanup-parent";
    private const int ForwardDays = 15;
    private const int RetainedTokens = 4;
    private const int RetainedAuthorizations = 2;

    /// <summary>Requires a capacity-denied first invocation to commit only cleanup, allowing the next independent invocation to reclaim its empty parent.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.FillAsync(first, AuthorizationLimit, true, OpenIddictConstants.AuthorizationTypes.AdHoc, OpenIddictConstants.Statuses.Valid, token);
        await PrepareParentAsync(first, token);
        Assert.False(await CleanupAndProbeAsync(first, token));
        Assert.Equal((0, AuthorizationLimit), await NativeTokenBudgetFixture.CountAsync(second, token));
        Assert.True(await CleanupAndProbeAsync(second, token));
        Assert.Equal((0, AuthorizationLimit - 1), await NativeTokenBudgetFixture.CountAsync(first, token));
        await VerifyRetentionAsync(first, second, token);
        await VerifyRollbackAsync(first, second, token);
    }

    /// <summary>A forward cleanup sample followed by forbidden rollback cannot commit deletion; an actual post-delete failure also rolls back.</summary>
    private static async Task VerifyRollbackAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        var clock = first.GetRequiredService<NativeConsumptionClock>();
        var original = clock.GetUtcNow();
        var before = await NativeTokenBudgetFixture.CountAsync(second, token);
        try
        {
            clock.Set(original.AddDays(ForwardDays));
            Assert.False(await NativeTokenBudgetFixture.WriteAsync(first, async (scope, cancellation) =>
            {
                var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
                var capacity = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, application, AdministratorTokenCreation.AuthorizationCode, TimeSpan.Zero, cancellation);
                Assert.NotNull(capacity);
                clock.Set(original);
                var allowed = await capacity.CanCommitCleanupAsync(cancellation);
                return (allowed, allowed);
            }, token));
            Assert.Equal(before, await NativeTokenBudgetFixture.CountAsync(second, token));
            clock.Set(original.AddDays(ForwardDays));
            var expected = new ArithmeticException();
            var fault = first.GetRequiredService<NativeTokenCleanupFault>();
            var calls = 0;
            fault.AfterDelete = () => { calls++; throw expected; };
            try
            {
                Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => CleanupAndProbeAsync(first, token)));
            }
            finally { fault.AfterDelete = null; }
            Assert.Equal(1, calls);
            Assert.Equal(before, await NativeTokenBudgetFixture.CountAsync(second, token));
        }
        finally { clock.Set(original); }
    }

    /// <summary>Builds one old parent with exactly a whole cleanup batch of expired children, retaining all other authorization rows as young.</summary>
    private static Task<bool> PrepareParentAsync(IServiceProvider root, CancellationToken token) => NativeTokenBudgetFixture.WriteAsync(root, async (scope, cancellation) =>
    {
        var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
        await scope.Owner.RunAsync(async current =>
        {
            var database = scope.Owner.Database;
            var parent = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().FirstAsync(row => row.Application!.Id == application, current);
            var now = scope.Owner.Clock.GetUtcNow().UtcDateTime;
            parent.CreationDate = now.AddDays(-RetentionDays).AddTicks(-1);
            var owner = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.Id == application, current);
            database.AddRange(Enumerable.Range(0, CleanupRows).Select(_ => new OpenIddictEntityFrameworkCoreToken
            {
                Application = owner,
                Authorization = parent,
                CreationDate = parent.CreationDate,
                ExpirationDate = now.AddSeconds(-GraceSeconds).AddTicks(-1),
                Type = OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
                Status = OpenIddictConstants.Statuses.Revoked
            }));
            await database.SaveChangesAsync(current);
        }, cancellation, write: true);
        return (true, true);
    }, token);

    /// <summary>Commits bounded cleanup before any native issuance occurs, even when the exact code creation budget remains full.</summary>
    private static Task<bool> CleanupAndProbeAsync(IServiceProvider root, CancellationToken token) => NativeTokenBudgetFixture.WriteAsync(root, async (scope, cancellation) =>
    {
        var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
        var capacity = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, application, AdministratorTokenCreation.AuthorizationCode, TimeSpan.Zero, cancellation);
        Assert.NotNull(capacity);
        return (capacity.HasCapacity, await capacity.CanCommitCleanupAsync(cancellation));
    }, token);

    /// <summary>Retains strict age/expiry equality, unknown expiry and live state; a foreign application's child prevents authorization deletion.</summary>
    private static async Task VerifyRetentionAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        await NativeTokenBudgetFixture.ClearAsync(first, token);
        await NativeTokenBudgetFixture.WriteAsync(first, async (scope, cancellation) =>
        {
            var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
            await scope.Owner.RunAsync(async current =>
            {
                var database = scope.Owner.Database;
                var owner = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.Id == application, current);
                AddRetainedRows(database, owner, scope.Owner.Clock.GetUtcNow().UtcDateTime);
                await database.SaveChangesAsync(current);
            }, cancellation, write: true);
            return (true, true);
        }, token);
        Assert.True(await CleanupAndProbeAsync(second, token));
        Assert.Equal((RetainedTokens, RetainedAuthorizations), await NativeTokenBudgetFixture.CountAsync(first, token));
        await NativeTokenBudgetFixture.WriteAsync(first, async (scope, cancellation) =>
        {
            await scope.Owner.RunAsync(async current =>
            {
                var foreign = scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Authorization!.Id == ParentId);
                Assert.Equal(1, await foreign.CountAsync(current));
                await foreign.ExecuteDeleteAsync(current);
            }, cancellation, write: true);
            return (true, true);
        }, token);
    }

    /// <summary>Creates independent test rows exactly at each conservative retention boundary plus a cross-application dependency.</summary>
    private static void AddRetainedRows(DbContext database, OpenIddictEntityFrameworkCoreApplication application, DateTime now)
    {
        var old = now.AddDays(-RetentionDays).AddTicks(-1);
        var expired = now.AddSeconds(-GraceSeconds).AddTicks(-1);
        var parent = new OpenIddictEntityFrameworkCoreAuthorization { Id = ParentId, Application = application, CreationDate = old, Type = OpenIddictConstants.AuthorizationTypes.AdHoc };
        database.Add(parent);
        database.Add(new OpenIddictEntityFrameworkCoreAuthorization { Application = application, CreationDate = old, Type = OpenIddictConstants.AuthorizationTypes.Permanent });
        foreach (var pair in new (DateTime Created, DateTime? Expires)[] { (old, now.AddSeconds(-GraceSeconds)), (now.AddDays(-RetentionDays), expired), (old, null), (old, now.AddSeconds(1)) })
        {
            database.Add(new OpenIddictEntityFrameworkCoreToken
            {
                Application = application,
                CreationDate = pair.Created,
                ExpirationDate = pair.Expires,
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
            });
        }
        database.Add(new OpenIddictEntityFrameworkCoreToken
        {
            Application = new OpenIddictEntityFrameworkCoreApplication(),
            Authorization = parent,
            CreationDate = old,
            ExpirationDate = expired,
            Status = OpenIddictConstants.Statuses.Revoked,
            Type = OpenIddictConstants.TokenTypeIdentifiers.AccessToken
        });
    }
}
