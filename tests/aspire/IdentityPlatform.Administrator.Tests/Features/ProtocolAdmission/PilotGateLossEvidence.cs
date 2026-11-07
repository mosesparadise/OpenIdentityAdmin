using System.Globalization;
using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Uses independent native connections to terminate exactly the observed gate owner and verify retained durable state.</summary>
internal static class PilotGateLossEvidence
{
    private const int CommandSeconds = 2;
    private const int IssuedChainEntries = 3;
    private const int CodeOnlyEntries = 1;
    private const string SqlTerminate = "KILL ";
    private const string PostgresTerminate = "SELECT pg_terminate_backend(@session)";
    private const string SessionParameter = "session";

    /// <summary>Terminates only the positively observed runtime backend using the disposable fixture administrator connection.</summary>
    internal static async Task TerminateAsync(BrowserHttpsFixture environment, int session)
    {
        Assert.True(session > 0);
        var token = TestContext.Current.CancellationToken;
        await using var scope = environment.Issuer.Account.Journey.Lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.Database.OpenConnectionAsync(token);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = CommandSeconds;
        if (database.Database.IsSqlServer())
        {
            command.CommandText = SqlTerminate + session.ToString(CultureInfo.InvariantCulture);
            await command.ExecuteNonQueryAsync(token);
        }
        else
        {
            command.CommandText = PostgresTerminate;
            var parameter = command.CreateParameter();
            parameter.ParameterName = SessionParameter;
            parameter.Value = session;
            command.Parameters.Add(parameter);
            Assert.Equal(true, await command.ExecuteScalarAsync(token));
        }
    }

    /// <summary>Commits native manager bulk revocation and proves that its concurrency token remains unchanged while actual status changes.</summary>
    internal static async Task RevokeWithoutConcurrencyChangeAsync(BrowserHttpsFixture environment, string codeId)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == codeId)
            .Select(row => new { row.ConcurrencyToken, row.Status, AuthorizationId = row.Authorization!.Id }).SingleAsync(token);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, before.Status);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        Assert.Equal(1L, await manager.RevokeByAuthorizationIdAsync(before.AuthorizationId!, token));
        var after = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == codeId)
            .Select(row => new { row.ConcurrencyToken, row.Status, row.RedemptionDate }).SingleAsync(token);
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, after.Status);
        Assert.Null(after.RedemptionDate);
    }

    /// <summary>Requires no native output in an infrastructure denial, retaining native invalid_grant where the child safely rejects first.</summary>
    internal static async Task RequireDeniedAsync(HttpResponseMessage response, bool infrastructure)
    {
        Assert.False(response.Headers.Contains(PilotGenerationFault.StagedHeader));
        if (infrastructure || response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }
        else
        {
            await InitialPilotReplayEvidence.RequireRejectedAsync(response);
        }
    }

    /// <summary>Checks the complete target inventory before any explicit follow-up replay could hide a late-child cleanup defect.</summary>
    internal static async Task RequireChainAsync(BrowserHttpsFixture environment, string codeId, bool issued)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var authorization = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == codeId)
            .Select(row => row.Authorization!.Id).SingleAsync(token);
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Authorization!.Id == authorization)
            .Select(row => new { row.Id, row.Type, row.Status }).ToArrayAsync(token);
        Assert.Equal(issued ? IssuedChainEntries : CodeOnlyEntries, rows.Length);
        var code = Assert.Single(rows, row => row.Id == codeId);
        Assert.Equal(issued ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Revoked, code.Status);
        if (issued)
        {
            Assert.Single(rows, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
            Assert.Single(rows, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
            Assert.All(rows.Where(row => row.Id != codeId), row => Assert.Equal(OpenIddictConstants.Statuses.Valid, row.Status));
        }
    }
}
