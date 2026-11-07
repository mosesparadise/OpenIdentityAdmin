using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Independently verifies exact audit fields and complete fixture-native catalogue state without exposing raw contents in failed snapshots.</summary>
internal static class CatalogueMutationEvidence
{
    private const string Actor = "User";
    private const string Accepted = "Accepted";

    /// <summary>Requires exactly one independently expected audit for the caller-owned correlation and original operation time.</summary>
    internal static async Task RequireAuditAsync(ApplicationMutationFixture test, Guid correlation, string action, string type, string target, long at)
    {
        await using var scope = test.Scope();
        var rows = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .Where(row => row.CorrelationId == correlation).ToArrayAsync(test.Token);
        var audit = Assert.Single(rows);
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(action, audit.Event);
        Assert.Equal(Actor, audit.ActorType);
        Assert.Equal(test.Identity.Subject, audit.ActorId);
        Assert.Equal(type, audit.TargetType);
        Assert.Equal(target, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.Equal(correlation, audit.CorrelationId);
        Assert.Equal(at, audit.OccurredAt);
        Assert.Null(audit.RelatedCorrelationId);
    }

    /// <summary>Requires the expected denial and exact unchanged native scopes, policy, audiences and audit history from fresh contexts.</summary>
    internal static async Task RequireDeniedAsync(ApplicationMutationFixture test, CatalogueIntent intent, int status)
    {
        var before = await SnapshotAsync(test);
        Assert.Equal(status, (await CatalogueMutationTests.ExecuteAsync(test, intent)).Status);
        Assert.Equal(before, await SnapshotAsync(test));
    }

    /// <summary>Includes all persisted native scope fields and every product/audit property, not just tracked or target-count observations.</summary>
    internal static async Task<string[]> SnapshotAsync(ApplicationMutationFixture test)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return
        [
            Hash(await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(test.Token)),
            Hash(await database.Set<ScopePolicy>().AsNoTracking().OrderBy(row => row.ScopeId).ToArrayAsync(test.Token)),
            Hash(await database.Set<ApiAudience>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(test.Token)),
            Hash(await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(test.Token))
        ];
    }

    /// <summary>Uses complete serialized values while preventing raw synthetic identity or policy fields from entering assertion diagnostics.</summary>
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
