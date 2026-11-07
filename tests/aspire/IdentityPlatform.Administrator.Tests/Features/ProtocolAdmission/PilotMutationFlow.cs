using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Supplies independent public-token and fresh native-storage oracles shared by the bounded pilot mutants.</summary>
internal static class PilotMutationFlow
{
    private const int TokenCount = 2;
    private const string WrongBinding = "The genuine pilot response changed its native authorization binding.";
    private const string PartialPersistence = "The native generation outcome was neither wholly committed nor wholly absent.";
    private const string WrongStatus = "The successful pilot response has unexpected persisted native status.";
    private static readonly string[] TokenNames = [OpenIddictConstants.Parameters.AccessToken, OpenIddictConstants.Parameters.IdToken];

    /// <summary>Identifies the one fresh native code created by the genuine password journey without decrypting or replacing it.</summary>
    internal static async Task<string> CodeAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow)
    {
        var token = TestContext.Current.CancellationToken;
        var identity = await client.ExpectedIdentity(flow, token);
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var codes = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row =>
            row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode && row.Status == OpenIddictConstants.Statuses.Valid &&
            row.Subject == identity.Item1).Select(row => row.Id!).ToArrayAsync(token);
        return Assert.Single(codes);
    }

    /// <summary>Checks actual native code lifecycle after the original request has completed, using a different scope and connection.</summary>
    internal static async Task<bool> IsUnconsumedAsync(BrowserHttpsFixture environment, string identifier)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .SingleAsync(value => value.Id == identifier, TestContext.Current.CancellationToken);
        Assert.True(row.ExpirationDate > DateTime.UtcNow);
        return row.Status == OpenIddictConstants.Statuses.Valid && row.RedemptionDate is null;
    }

    /// <summary>Validates the actual successful public response, then distinguishes committed native entries from a publication of rolled-back entries.</summary>
    internal static async Task<bool> IsCommittedSuccessAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow, HttpResponseMessage response)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, token));
        var outputs = TokenNames.Select(name => new JsonWebToken(body.RootElement.GetProperty(name).GetString()!)).ToArray();
        var identifiers = outputs.Select(value => value.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value).ToArray();
        Assert.Equal(TokenCount, identifiers.Distinct(StringComparer.Ordinal).Count());
        var authorization = outputs[0].GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value;
        Assert.True(outputs.All(value => value.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value == authorization), WrongBinding);
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => identifiers.Contains(row.Id!))
            .Select(row => new { row.Status, Authorization = row.Authorization!.Id }).ToArrayAsync(token);
        Assert.True(rows.Length is 0 or TokenCount, PartialPersistence);
        Assert.True(rows.All(row => row.Status == OpenIddictConstants.Statuses.Valid && row.Authorization == authorization), WrongStatus);
        return rows.Length == TokenCount;
    }

    /// <summary>Requires a new genuine flow to remain available after the attacked request and its owned cleanup finish.</summary>
    internal static async Task RequireFreshAsync(BrowserHttpsFixture environment, TestRelyingParty client)
    {
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, client);
        using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await IsCommittedSuccessAsync(environment, client, flow, response));
    }
}
