using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains the actual successful exchange's native lineage and independently witnesses committed replay state before publication fails.</summary>
internal sealed class PilotReplayPublicationEvidence(string codeId, string accessId, string authorizationId)
{
    private const int ChainEntries = 3;
    private string? committedDigest;
    private string CodeId => codeId;
    private string AccessId => accessId;
    internal string AuthorizationId => authorizationId;

    /// <summary>Completes and independently validates a genuine exchange, leaving its code Redeemed and access entry Valid without fixture status mutation.</summary>
    internal static async Task<PilotReplayPublicationEvidence> CreateAsync(BrowserHttpsFixture environment, TestRelyingParty client,
        TestFlow flow, PilotCodeObservation observation)
    {
        var token = TestContext.Current.CancellationToken;
        using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, token));
        Assert.NotNull(observation.Principal);
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        var evidence = new PilotReplayPublicationEvidence(observation.Principal.GetTokenId()!,
            access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value,
            access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value);
        var sameAuthorization = string.Equals(evidence.AuthorizationId, observation.Principal.GetAuthorizationId(), StringComparison.Ordinal);
        Assert.True(sameAuthorization);
        await PilotReplayHistory.RequireStatusAsync(environment, evidence.CodeId, OpenIddictConstants.Statuses.Redeemed);
        await PilotReplayHistory.RequireStatusAsync(environment, evidence.AccessId, OpenIddictConstants.Statuses.Valid);
        return evidence;
    }

    /// <summary>Uses a fresh connection outside the HTTP execution context, proving revocation is committed before allowing the injected publication failure.</summary>
    internal async Task RequireCommittedAsync(BrowserHttpsFixture environment, CancellationToken token)
    {
        Assert.Null(committedDigest);
        await RequireRevokedAsync(environment, token);
        committedDigest = await DigestAsync(environment, token);
    }

    /// <summary>Checks that error formatting and cleanup did not undo revocation, add native entries or mutate another authorization.</summary>
    internal async Task RequireRetainedAsync(BrowserHttpsFixture environment, CancellationToken token)
    {
        Assert.NotNull(committedDigest);
        await RequireRevokedAsync(environment, token);
        Assert.Equal(committedDigest, await DigestAsync(environment, token));
    }

    /// <summary>Requires the genuine code, access and identity entries all remain in the same exact revoked native chain.</summary>
    private async Task RequireRevokedAsync(BrowserHttpsFixture environment, CancellationToken token)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var rows = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Authorization!.Id == authorizationId).Select(row => new { row.Id, row.Type, row.Status }).ToArrayAsync(token);
        Assert.Equal(ChainEntries, rows.Length);
        var hasCode = rows.Any(row => row.Id == codeId && row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var hasAccess = rows.Any(row => row.Id == accessId && row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        Assert.True(hasCode && hasAccess);
        var identityCount = rows.Count(row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        Assert.Equal(1, identityCount);
        foreach (var row in rows)
        {
            Assert.Equal(OpenIddictConstants.Statuses.Revoked, row.Status);
        }
    }

    /// <summary>Uses explicitly captured cancellation because the fault's observer deliberately does not inherit ambient test or protocol context.</summary>
    private static async Task<string> DigestAsync(BrowserHttpsFixture environment, CancellationToken token)
    {
        await using var scope = PilotReplayHistory.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).Select(row => new
        {
            row.Id,
            Application = row.Application == null ? null : row.Application.Id,
            Authorization = row.Authorization == null ? null : row.Authorization.Id,
            row.ConcurrencyToken,
            row.CreationDate,
            row.ExpirationDate,
            row.Payload,
            row.Properties,
            row.RedemptionDate,
            row.Status,
            row.Subject,
            row.Type
        }).ToArrayAsync(token);
        return FactorBinding.Digest(new
        {
            Tokens = tokens,
            Authorizations = await AuthorizationDigestAsync(database, token)
        });
    }

    /// <summary>Includes complete native authorization scalars without producing printable row identifiers or payloads.</summary>
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
