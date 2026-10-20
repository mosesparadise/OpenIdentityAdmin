using System.Globalization;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Seeds disposable state budgets in bounded independent fixture transactions without weakening production deadlines.</summary>
internal static class NativeClientStateQuotaFixture
{
    internal enum Kind
    {
        Live, Retained, ExpiredMixedCase, Foreign
    }
    private const int SeedBatch = 250;
    private const int ExpiredDays = 20;
    private const int FutureSeconds = 300;
    private const int CaseInterval = 2;
    private const string ForeignType = "unrelated-native-token";
    private const string ForeignId = "000-foreign";
    private const string IndexFormat = "D5";

    /// <summary>Creates manager-shaped capacity rows with explicit status/expiry/type; only production native generation creates tested output.</summary>
    internal static async Task FillAsync(IServiceProvider root, int count, Kind kind, CancellationToken token)
    {
        for (var index = 0; index < count; index += SeedBatch)
        {
            var start = index;
            var size = Math.Min(SeedBatch, count - index);
            await WriteAsync(root, (scope, cancellation) => scope.Owner.RunAsync(async current =>
            {
                var now = scope.Owner.Clock.GetUtcNow().UtcDateTime;
                for (var offset = 0; offset < size; offset++)
                {
                    var number = start + offset;
                    scope.Owner.Database.Add(Row(kind, number, now));
                }
                await scope.Owner.Database.SaveChangesAsync(current);
            }, cancellation, write: true), token);
        }
    }

    /// <summary>Gives foreign rows a deterministic first-sort position so cleanup must inspect and retain them within its bounded batch.</summary>
    private static OpenIddictEntityFrameworkCoreToken Row(Kind kind, int index, DateTime now)
    {
        var old = kind is Kind.ExpiredMixedCase or Kind.Foreign;
        var type = kind == Kind.Foreign ? ForeignType : OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken;
        return new()
        {
            Id = kind == Kind.Foreign ? ForeignId : kind + index.ToString(IndexFormat, CultureInfo.InvariantCulture),
            CreationDate = old ? now.AddDays(-ExpiredDays) : now,
            ExpirationDate = old ? now.AddDays(-ExpiredDays) : now.AddSeconds(FutureSeconds),
            Type = kind == Kind.ExpiredMixedCase && index % CaseInterval == 0 ? type.ToUpperInvariant() : type,
            Status = kind == Kind.Retained ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Valid
        };
    }

    /// <summary>Acquires the same Sessions coordination used by real native creation and commits only fixture setup work.</summary>
    internal static Task<bool> WriteAsync(IServiceProvider root, Func<ProtocolStorageScope, CancellationToken, ValueTask> operation, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            await operation(scope, cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Independently verifies the unrelated row remains intact after each bounded production cleanup.</summary>
    internal static Task<bool> HasForeignAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), (scope, cancellation) =>
            scope.Owner.RunAsync(current => new ValueTask<bool>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>()
                .AnyAsync(row => row.Id == ForeignId && row.Type == ForeignType, current)), cancellation));
}
