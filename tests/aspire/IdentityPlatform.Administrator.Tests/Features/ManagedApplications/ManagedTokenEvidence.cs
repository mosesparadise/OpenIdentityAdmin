using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Checks bounded native errors and independently committed managed issuance state without printing secret-bearing rows.</summary>
internal static class ManagedTokenEvidence
{
    private const string SetCookie = "Set-Cookie";
    private const string Error = "error";
    private const string Description = "error_description";
    private const string ErrorUri = "error_uri";
    private const string LocalDescription = "error_description:The request could not be completed.\n";
    private const string ErrorPrefix = "error:";
    private const string NewLine = "\n";
    private const string TextMedia = "text/plain";
    private const string JsonMedia = "application/json";
    private const string CodeAudit = "managed-code-issued";
    private const string TokenAudit = "managed-token-issued";
    private const string AccountActor = "account";
    private const string ApplicationTarget = "application";
    private const string Accepted = "accepted";

    /// <summary>Requires a native OAuth JSON error with no response credential, redirect or cookie.</summary>
    internal static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error, bool local = false)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(local ? TextMedia : JsonMedia, response.Content.Headers.ContentType?.MediaType);
        if (local)
        {
            Assert.Equal(ErrorPrefix + error + NewLine + LocalDescription, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return;
        }
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.EnumerateObject().All(field => field.Name is Error or Description or ErrorUri));
        Assert.True(body.RootElement.GetProperty(Error).GetString() == error);
    }

    /// <summary>Hashes whole native descriptors, interaction rows and audit history while excluding only deliberate durable redemption metadata.</summary>
    internal static async Task<string> RollbackDigestAsync(ManagedCacheTestContext test, long? observed = null)
    {
        var snapshot = await test.SnapshotAsync();
        if (observed is { } decisionWatermark)
        {
            Assert.Single(snapshot.Interactions).ObservedAt = decisionWatermark;
        }
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        var interaction = Assert.Single(snapshot.Interactions);
        var consumed = interaction.State == ManagedLoginState.Consumed ? interaction.CodeId : interaction.RequestId;
        var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == test.Id)
            .OrderBy(row => row.Id).Select(row => new
            {
                row.Id,
                Application = row.Application!.Id,
                row.ConcurrencyToken,
                row.CreationDate,
                row.Properties,
                row.Scopes,
                row.Status,
                row.Subject,
                row.Type
            }).ToArrayAsync(TestContext.Current.CancellationToken);
        return FactorBinding.Digest(new
        {
            snapshot.Interactions,
            Tokens = snapshot.Tokens.Select(row => Token(row, test.Id, consumed)),
            Authorizations = authorizations,
            Audit = audits
        });
    }

    /// <summary>Allows only the designated input's native redemption transition; every other descriptor field and token status stays exact.</summary>
    private static object Token(OpenIddictEntityFrameworkCoreToken row, string application, string? consumed) => new
    {
        row.Id,
        Application = application,
        Authorization = row.Authorization?.Id,
        Concurrency = row.Id == consumed ? null : row.ConcurrencyToken,
        row.CreationDate,
        row.ExpirationDate,
        row.Payload,
        row.Properties,
        Redemption = row.Id == consumed ? null : row.RedemptionDate,
        row.ReferenceId,
        Status = row.Id == consumed ? null : row.Status,
        row.Subject,
        row.Type
    };

    /// <summary>Counts actual committed exchange audits independently of HTTP publication and native token row counts.</summary>
    internal static async Task<int> ExchangeAuditsAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
            .CountAsync(row => row.Event == TokenAudit && row.TargetId == test.Id, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads one exact native authorization independently, including native replay's deliberate preservation of the authorization itself.</summary>
    internal static async Task<string?> AuthorizationStatusAsync(ManagedCacheTestContext test, string identifier)
    {
        await using var scope = test.Http.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .AsNoTracking().Where(row => row.Id == identifier).Select(row => row.Status).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Requires one exact sanitized accepted audit for each successful native issuance phase and the original managed correlation.</summary>
    internal static async Task IssuanceAuditAsync(ManagedCacheTestContext test, bool applicationOnly = false)
    {
        var interaction = Assert.Single((await test.SnapshotAsync(applicationOnly)).Interactions);
        await using var scope = test.Http.Scope();
        var audits = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .Where(row => (!applicationOnly || row.TargetId == test.Id) && (row.Event == CodeAudit || row.Event == TokenAudit)).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(audits, row => row.Event == CodeAudit);
        Assert.Single(audits, row => row.Event == TokenAudit);
        Assert.All(audits, row => Assert.True(row.ActorType == AccountActor && row.ActorId == interaction.UserId &&
            row.TargetType == ApplicationTarget && row.TargetId == test.Id && row.Outcome == Accepted &&
            row.CorrelationId == interaction.CorrelationId && row.OccurredAt >= interaction.ConsentVerifiedAt && row.OccurredAt < interaction.ExpiresAt));
    }
}
