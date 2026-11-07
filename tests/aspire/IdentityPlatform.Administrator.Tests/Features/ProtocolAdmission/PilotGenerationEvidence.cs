using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains exact original code and native inventory so rollback and committed-output loss have distinct independent oracles.</summary>
internal sealed class PilotGenerationEvidence(string codeId, string authorizationId, string[] identifiers, string authorizations)
{
    internal string CodeId => codeId;
    private const string WrongInventory = "Pilot generation changed an unexpected native token inventory.";
    private const int CommittedOutputs = 2;

    /// <summary>Freezes the genuine unspent code, complete token inventory and authorization rows before arming the generation fault.</summary>
    internal static async Task<PilotGenerationEvidence> BeforeAsync(BrowserHttpsFixture environment, string subject, CancellationToken token)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var code = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Subject == subject && row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode)
            .Select(row => new { row.Id, AuthorizationId = row.Authorization!.Id, row.Status, row.RedemptionDate }).SingleAsync(token);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, code.Status);
        Assert.Null(code.RedemptionDate);
        Assert.False(string.IsNullOrEmpty(code.AuthorizationId));
        var identifiers = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id!).ToArrayAsync(token);
        return new(code.Id!, code.AuthorizationId!, identifiers, await AuthorizationDigestAsync(database, token));
    }

    /// <summary>A separate observer sees durable redemption while generation is still active, using an explicitly carried test cancellation token.</summary>
    internal async Task RequireConsumedAsync(BrowserHttpsFixture environment, CancellationToken token)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var code = await manager.FindByIdAsync(codeId, token);
        Assert.NotNull(code);
        Assert.True(await manager.HasStatusAsync(code, OpenIddictConstants.Statuses.Redeemed, token));
        Assert.NotNull(await manager.GetRedemptionDateAsync(code, token));
        Assert.Equal(authorizationId, await manager.GetAuthorizationIdAsync(code, token));
    }

    /// <summary>Distinguishes rollback after INSERT from an actually committed access/identity pair whose HTTP publication was suppressed.</summary>
    internal async Task RequireOutcomeAsync(BrowserHttpsFixture environment, PilotGenerationFault fault, bool committed, CancellationToken token)
    {
        await RequireConsumedAsync(environment, token);
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Type, row.Status, AuthorizationId = row.Authorization == null ? null : row.Authorization.Id }).ToArrayAsync(token);
        Assert.True(identifiers.All(id => rows.Any(row => row.Id == id)), WrongInventory);
        var added = rows.Where(row => !identifiers.Contains(row.Id, StringComparer.Ordinal)).ToArray();
        Assert.Equal(committed ? CommittedOutputs : 0, added.Length);
        if (committed)
        {
            Assert.True(added.Any(row => row.Id == fault.AccessId && row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken), WrongInventory);
            Assert.Single(added, row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
            Assert.All(added, row => Assert.True(row.Status == OpenIddictConstants.Statuses.Valid && row.AuthorizationId == authorizationId, WrongInventory));
        }
        Assert.Equal(authorizations, await AuthorizationDigestAsync(database, token));
    }

    /// <summary>After a separate replay, every retained target entry is revoked and no retry has generated another token pair.</summary>
    internal async Task RequireReplayedAsync(BrowserHttpsFixture environment, bool committed, CancellationToken token)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Authorization!.Id == authorizationId).Select(row => new { row.Id, row.Status }).ToArrayAsync(token);
        Assert.Equal(committed ? CommittedOutputs + 1 : 1, rows.Length);
        Assert.Contains(rows, row => row.Id == codeId);
        Assert.All(rows, row => Assert.Equal(OpenIddictConstants.Statuses.Revoked, row.Status));
        Assert.Equal(identifiers.Length + (committed ? CommittedOutputs : 0), await database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(token));
    }

    /// <summary>Hashes complete native authorization scalars and bindings without rendering identifiers or payloads on failure.</summary>
    private static async Task<string> AuthorizationDigestAsync(PlatformDbContext database, CancellationToken token) => FactorBinding.Digest(
        await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(row => row.Id).Select(row => new
        {
            row.Id,
            Application = row.Application == null ? null : row.Application.Id,
            row.ConcurrencyToken,
            row.CreationDate,
            row.Properties,
            row.Scopes,
            row.Status,
            row.Subject,
            row.Type
        }).ToArrayAsync(token));
}
