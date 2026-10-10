using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Sensitive values stay in memory; assertions never print native entries or their identifiers.
public sealed class ExchangeEvidence
{
    public required TestFlow Flow
    {
        get; init;
    }
    public required string CodeId
    {
        get; init;
    }
    public required string Subject
    {
        get; init;
    }
    public required string ApplicationId
    {
        get; init;
    }
    public required string AuthorizationId
    {
        get; init;
    }
    public required string SessionId
    {
        get; init;
    }
    public required long AuthenticatedAt
    {
        get; init;
    }
    public required string SecurityStamp
    {
        get; init;
    }

    public static async Task<ExchangeEvidence> IssueAsync(IssuerEnvironment environment, HttpClient browser)
    {
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        var flow = await environment.AuthorizeAsync(browser);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var added = (await Tokens(database).ToArrayAsync(TestContext.Current.CancellationToken))
            .Where(value => !before.TokenIds.Contains(value.Id!) && value.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode).ToArray();
        Assert.True(added.Length == 1,
            "A fresh flow must create exactly one native authorization code.");
        var code = added[0];
        var manifest = await database.Set<PilotManifest>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(code.Subject == manifest.UserId && code.Application?.Id == manifest.ApplicationId && code.Authorization?.Id is not null,
            "The selected code must belong to the expected subject, client and native authorization.");
        var session = await database.Set<LocalIdentitySession>().AsNoTracking()
            .SingleAsync(value => value.Id == flow.ExpectedSessionId, TestContext.Current.CancellationToken);
        Assert.True(session.UserId == code.Subject && session.AuthenticatedAt == flow.ExpectedAuthenticatedAt,
            "The code flow must preserve its completed login identity and time.");
        var evidence = new ExchangeEvidence
        {
            Flow = flow,
            CodeId = code.Id!,
            Subject = code.Subject!,
            ApplicationId = code.Application!.Id!,
            AuthorizationId = code.Authorization!.Id!,
            SessionId = session.Id,
            AuthenticatedAt = session.AuthenticatedAt,
            SecurityStamp = session.SecurityStamp
        };
        await evidence.RequireFreshAsync(environment);
        return evidence;
    }

    public async Task RequireFreshAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var code = await Tokens(database).SingleAsync(value => value.Id == CodeId, TestContext.Current.CancellationToken);
        Assert.True(code.Status == OpenIddictConstants.Statuses.Valid && code.RedemptionDate is null &&
            code.ExpirationDate > DateTime.UtcNow.AddSeconds(10) && code.Subject == Subject &&
            code.Application?.Id == ApplicationId && code.Authorization?.Id == AuthorizationId,
            "The exact bound code must remain unredeemed and comfortably unexpired.");
        await RequireSessionAsync(environment);
    }

    public async Task RequireSessionAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await database.Set<LocalIdentitySession>().AsNoTracking()
            .SingleAsync(value => value.Id == SessionId, TestContext.Current.CancellationToken);
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(value => value.Id == Subject, TestContext.Current.CancellationToken);
        Assert.True(session.UserId == Subject && session.AuthenticatedAt == AuthenticatedAt &&
            session.SecurityStamp == SecurityStamp && !session.Revoked && session.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10 &&
            (user is null || user.SecurityStamp == SecurityStamp), "Unrelated session and stamp state must remain unchanged.");
    }

    public static IQueryable<OpenIddictEntityFrameworkCoreToken> Tokens(PlatformDbContext database) =>
        database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Include(value => value.Application).Include(value => value.Authorization);

    public async Task AssertDeniedAsync(IssuerEnvironment environment, HttpResponseMessage response, ExchangeSnapshot before)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Location is null, "Denied exchange must not redirect.");
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", "Denied exchange must return JSON.");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SafeAssertions.ContainsNoSensitiveMaterial(text, Flow.Code, Flow.Verifier, environment.Password, environment.ClientSecret);
        using var body = JsonDocument.Parse(text);
        Assert.True(body.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String &&
            error.GetString() == "invalid_grant", "Denied exchange must return the expected protocol error.");
        foreach (var property in new[] { "access_token", "id_token", "refresh_token" })
        {
            Assert.False(body.RootElement.TryGetProperty(property, out _));
        }
        var after = await ExchangeSnapshot.CaptureAsync(environment);
        Assert.True(before.TokenIds.SetEquals(after.TokenIds), "Denied exchange must not add or delete native token rows.");
        before.AssertAuditUnchanged(after);
    }
}

public sealed class ExchangeSnapshot
{
    public required HashSet<string> TokenIds
    {
        get; init;
    }
    public required Dictionary<Guid, string> Audits
    {
        get; init;
    }

    public static async Task<ExchangeSnapshot> CaptureAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().Select(value => value.Id!).ToArrayAsync(TestContext.Current.CancellationToken);
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        return new ExchangeSnapshot
        {
            TokenIds = tokens.ToHashSet(StringComparer.Ordinal),
            Audits = audits.ToDictionary(value => value.Id, value => JsonSerializer.Serialize(value))
        };
    }

    public void AssertAuditUnchanged(ExchangeSnapshot after) =>
        Assert.True(Audits.Count == after.Audits.Count && Audits.All(value => after.Audits.TryGetValue(value.Key, out var current) && current == value.Value),
            "Exchange must preserve the complete committed audit history.");
}
