using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Reads native outcomes independently of the ordering trigger and validates actual public credentials.</summary>
internal static class PilotReplayDistributionEvidence
{
    private const int CodeLifetimeSeconds = 60;
    private const string OriginalLifetime = "The original code lifetime changed.";
    private const string DurableConsumption = "Winning consumption must be durable while the original code remains unexpired.";
    private const string OriginalAuthorization = "The winning public tokens must retain the original native authorization.";
    private const string OnlyWinningCredentials = "Concurrent nodes persisted credentials beyond the independently validated winning response.";
    private const string RevokedLiveCode = "Initial replay must revoke the original still-live consumed code.";
    private const string ExactLinkedEntries = "The native authorization contains unexpected generated entries.";
    private const string InitialReplaySafety = "Initial cross-process replay left a winning native entry usable before any additional replay.";
    private const string NoReplayCredentials = "The native replay response must not publish credentials.";
    private const string UnrelatedAuthority = "Unrelated native authority must remain usable after the ordered replay.";
    private static readonly string[] TokenNames = ["access_token", "id_token"];

    /// <summary>Records the original live code lifetime without extending it or preparing a replacement code.</summary>
    internal static async Task<DateTime> ExpirationAsync(IssuerEnvironment environment, ExchangeEvidence evidence)
    {
        await evidence.RequireFreshAsync(environment);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var code = await ExchangeEvidence.Tokens(database).SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
        Assert.True(code.ExpirationDate is not null && code.CreationDate is not null &&
            code.ExpirationDate - code.CreationDate <= TimeSpan.FromSeconds(CodeLifetimeSeconds), OriginalLifetime);
        return code.ExpirationDate.Value;
    }

    /// <summary>Observes committed consumption through another connection while generation is still blocked inside the actual INSERT.</summary>
    internal static async Task RequireConsumedAsync(IssuerEnvironment environment, ExchangeEvidence evidence, DateTime expiration)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var code = await ExchangeEvidence.Tokens(database).SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
        Assert.True(code.Status == OpenIddictConstants.Statuses.Redeemed && code.RedemptionDate is not null &&
            code.ExpirationDate == expiration && expiration > DateTime.UtcNow,
            DurableConsumption);
    }

    /// <summary>Requires exactly one real success, native denial and no usable winning entry before any later replay.</summary>
    internal static async Task RequireOutcomeAsync(IssuerEnvironment environment, ExchangeEvidence evidence, DateTime expiration,
        HttpResponseMessage winner, HttpResponseMessage replay, HttpClient transport, ExchangeSnapshot before)
    {
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        await RequireReplayAsync(replay);
        using var tokens = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, evidence.Flow, TestContext.Current.CancellationToken, transport));
        var ids = TokenNames.Select(name => new JsonWebToken(tokens.RootElement.GetProperty(name).GetString()!)).ToArray();
        Assert.True(ids.All(token => token.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value == evidence.AuthorizationId),
            OriginalAuthorization);
        var issued = ids.Select(token => token.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(TokenNames.Length, issued.Count);
        await RequireRevokedAsync(environment, evidence, expiration, issued);
        var after = await ExchangeSnapshot.CaptureAsync(environment);
        Assert.True(after.TokenIds.Except(before.TokenIds).ToHashSet(StringComparer.Ordinal).SetEquals(issued),
            OnlyWinningCredentials);
        before.AssertAuditUnchanged(after);
    }

    /// <summary>Checks every linked native entry, including unreturned extras, immediately after the initial competing replay.</summary>
    private static async Task RequireRevokedAsync(IssuerEnvironment environment, ExchangeEvidence evidence, DateTime expiration, HashSet<string> issued)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var linked = await ExchangeEvidence.Tokens(database).Where(value => value.Authorization!.Id == evidence.AuthorizationId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var code = Assert.Single(linked, value => value.Id == evidence.CodeId);
        Assert.True(code.Status == OpenIddictConstants.Statuses.Revoked && code.RedemptionDate is not null &&
            code.ExpirationDate == expiration && expiration > DateTime.UtcNow, RevokedLiveCode);
        var generated = linked.Where(value => value.Id != evidence.CodeId).ToArray();
        Assert.True(generated.Select(value => value.Id!).ToHashSet(StringComparer.Ordinal).SetEquals(issued),
            ExactLinkedEntries);
        Assert.True(generated.All(value => value.Status == OpenIddictConstants.Statuses.Revoked),
            InitialReplaySafety);
    }

    /// <summary>Requires the native replay error with no returned credentials or redirect.</summary>
    private static async Task RequireReplayAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString() == OpenIddictConstants.Errors.InvalidGrant);
        Assert.True(TokenNames.Append(OpenIddictConstants.Parameters.RefreshToken).All(name => !body.RootElement.TryGetProperty(name, out _)),
            NoReplayCredentials);
    }

    /// <summary>A different genuine authorization succeeds through the peer node after target-chain revocation.</summary>
    internal static async Task RequireControlAsync(IssuerEnvironment environment, ExchangeEvidence control, string targetAuthorization, string node)
    {
        Assert.False(string.Equals(control.AuthorizationId, targetAuthorization, StringComparison.Ordinal));
        await control.RequireFreshAsync(environment);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, node, control.Flow);
        var identifier = new JsonWebToken(tokens.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!)
            .GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value;
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var access = await ExchangeEvidence.Tokens(database).SingleAsync(value => value.Id == identifier, TestContext.Current.CancellationToken);
        Assert.True(access.Status == OpenIddictConstants.Statuses.Valid && access.Authorization!.Id == control.AuthorizationId,
            UnrelatedAuthority);
    }
}
