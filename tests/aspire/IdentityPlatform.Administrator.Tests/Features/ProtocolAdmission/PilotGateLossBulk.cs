using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Runs real native bulk revocation while the old child retains its already-validated tracked code.</summary>
internal sealed class PilotGateLossBulk(BrowserHttpsFixture environment, string codeId, string authorizationId, string? concurrency)
{
    internal bool UnchangedConcurrency
    {
        get; private set;
    }

    /// <summary>Reads immutable binding before either writer starts, so SQL Server does not block this setup read behind the held Valid-row lock.</summary>
    internal static async Task<PilotGateLossBulk> BeforeAsync(BrowserHttpsFixture environment, string codeId)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == codeId)
            .Select(row => new { row.ConcurrencyToken, AuthorizationId = row.Authorization!.Id }).SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrEmpty(row.AuthorizationId));
        return new(environment, codeId, row.AuthorizationId!, row.ConcurrencyToken);
    }

    /// <summary>Retains a response-first concurrency-token oracle while allowing legitimate prior redemption in the lock-wait trace.</summary>
    internal async Task RunAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        Assert.Equal(1L, await manager.RevokeByAuthorizationIdAsync(authorizationId, token));
        var after = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == codeId)
            .Select(row => row.ConcurrencyToken).SingleAsync(token);
        UnchangedConcurrency = string.Equals(concurrency, after, StringComparison.Ordinal);
        await transaction.CommitAsync(token);
    }
}
