using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Observes every scalar native/policy/audit field; secret-bearing comparisons expose only digests to assertion diagnostics.</summary>
internal sealed record ApplicationUpdateSnapshot(string Digest, string NativeDigest, string StableNativeDigest, string SecretDigest,
    string? Stamp, string? ClientId, string? Display, string[] Redirects, ApplicationPolicy Policy, SecurityAuditIntent[] Audits, ApplicationLifecycleAuditDetail[] LifecycleAudits)
{
    private const string Target = "Application";
    private static readonly string[] MutableNative = ["DisplayName", "RedirectUris", "ClientSecret", "ConcurrencyToken"];

    /// <summary>Reads a new detached snapshot after the production operation has disposed its owned context.</summary>
    internal static async Task<ApplicationUpdateSnapshot> ReadAsync(ApplicationMutationFixture test, string id)
    {
        await using var scope = test.Scope();
        return await ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), id, test.Token);
    }

    /// <summary>Queries persisted rows even inside a native-save interceptor, avoiding tracked values as evidence of SQL completion.</summary>
    internal static async Task<ApplicationUpdateSnapshot> ReadAsync(PlatformDbContext database, string id, CancellationToken token)
    {
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == id, token);
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == id, token);
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.TargetType == Target).OrderBy(row => row.Id).ToArrayAsync(token);
        var lifecycle = await database.Set<ApplicationLifecycleAuditDetail>().AsNoTracking().OrderBy(row => row.AuditId).ToArrayAsync(token);
        var fields = database.Entry(native).Properties.OrderBy(value => value.Metadata.Name, StringComparer.Ordinal)
            .ToDictionary(value => value.Metadata.Name, value => value.CurrentValue);
        var stable = fields.Where(value => !MutableNative.Contains(value.Key, StringComparer.Ordinal)).ToDictionary();
        return new(Hash(new
        {
            fields,
            policy,
            audits,
            lifecycle
        }), Hash(fields), Hash(stable), Hash(native.ClientSecret), native.ConcurrencyToken,
            native.ClientId, native.DisplayName, JsonSerializer.Deserialize<string[]>(native.RedirectUris!)!, policy, audits, lifecycle);
    }

    /// <summary>Hashes canonical in-process observations without logging native hashes or plaintext credentials.</summary>
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
