using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Provides bounded disposable native-row setup and actual manager creation for both-provider quota controls.</summary>
internal static class NativeTokenBudgetFixture
{
    private const int SeedBatch = 250;
    private const int FutureSeconds = 60;

    /// <summary>Runs one new profile-locked transaction with an explicit commit decision and unchanged request/write deadlines.</summary>
    internal static Task<T> WriteAsync<T>(IServiceProvider root, Func<ProtocolStorageScope, CancellationToken, ValueTask<(T Result, bool Commit)>> operation,
        CancellationToken token, Action<ProtocolStorageScope>? beforeLock = null) => new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token)
        .RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            beforeLock?.Invoke(scope);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var result = await operation(scope, cancellation);
            if (result.Commit)
            {
                await transaction.CommitAsync(cancellation);
            }
            return result.Result;
        });

    /// <summary>Resolves only the actual initialized application identity in the current owned context.</summary>
    internal static ValueTask<string> ApplicationAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async cancellation =>
        (await scope.Owner.Database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(cancellation)).ApplicationId, token);

    /// <summary>Starts a new clock scenario no earlier than the watermark deliberately advanced by preceding native ledger controls.</summary>
    internal static Task<DateTimeOffset> ObserveAsync(IServiceProvider root, CancellationToken token) => WriteAsync(root, async (scope, cancellation) =>
    {
        var watermark = await scope.Owner.RunAsync(async current =>
            (await scope.Owner.Database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(current)).ObservedAt, cancellation);
        return (DateTimeOffset.FromUnixTimeSeconds(Math.Max(watermark, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds())), false);
    }, token);

    /// <summary>Removes only the disposable fixed application's fixture rows after preceding conformance scenarios have finished.</summary>
    internal static Task<bool> ClearAsync(IServiceProvider root, CancellationToken token) => WriteAsync(root, async (scope, cancellation) =>
    {
        var application = await ApplicationAsync(scope, cancellation);
        await scope.Owner.RunAsync(async current =>
        {
            await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Application!.Id == application).ExecuteDeleteAsync(current);
            await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(row => row.Application!.Id == application).ExecuteDeleteAsync(current);
        }, cancellation, write: true);
        return (true, true);
    }, token);

    /// <summary>Seeds retained capacity in separate small fixture transactions, without extending production ownership budgets.</summary>
    internal static async Task FillAsync(IServiceProvider root, int count, bool authorizations, string type, string status, CancellationToken token)
    {
        for (var index = 0; index < count; index += SeedBatch)
        {
            var size = Math.Min(SeedBatch, count - index);
            await WriteAsync(root, async (scope, cancellation) =>
            {
                var application = await ApplicationAsync(scope, cancellation);
                await scope.Owner.RunAsync(async current =>
                {
                    var database = scope.Owner.Database;
                    var parent = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.Id == application, current);
                    var now = scope.Owner.Clock.GetUtcNow().UtcDateTime;
                    for (var offset = 0; offset < size; offset++)
                    {
                        if (authorizations)
                        {
                            database.Add(new OpenIddictEntityFrameworkCoreAuthorization { Application = parent, CreationDate = now, Type = type, Status = status });
                        }
                        else
                        {
                            database.Add(new OpenIddictEntityFrameworkCoreToken { Application = parent, CreationDate = now, ExpirationDate = now.AddSeconds(FutureSeconds), Type = type, Status = status });
                        }
                    }
                    await database.SaveChangesAsync(current);
                }, cancellation, write: true);
                return (true, true);
            }, token);
        }
    }

    /// <summary>Creates actual persisted tokens through the installed native manager inside the reserved issuance transaction.</summary>
    internal static async Task CreateTokensAsync(ProtocolStorageScope scope, string application, int count, string type, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        for (var index = 0; index < count; index++)
        {
            await manager.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = application,
                CreationDate = scope.Owner.Clock.GetUtcNow(),
                ExpirationDate = scope.Owner.Clock.GetUtcNow().AddSeconds(FutureSeconds),
                Type = type,
                Status = OpenIddictConstants.Statuses.Valid
            }, token);
        }
    }

    /// <summary>Reads retained token/authorization counts from an independent owned context after actual transaction cleanup.</summary>
    internal static Task<(int Tokens, int Authorizations)> CountAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var application = await ApplicationAsync(scope, cancellation);
            return await scope.Owner.RunAsync(async current =>
                (await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(row => row.Application!.Id == application, current),
                 await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync(row => row.Application!.Id == application, current)), cancellation);
        });
}
