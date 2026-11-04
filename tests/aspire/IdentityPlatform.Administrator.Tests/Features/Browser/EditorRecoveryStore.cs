using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Independently observes real committed application effects and changes only existing test-account grants.</summary>
internal static class EditorRecoveryStore
{
    private const string ApplicationTarget = "Application";
    private const int GeneratedIdentifier = 0;

    /// <summary>Finds the actual UI-created registration without supplying or fabricating an application ID.</summary>
    internal static async Task<string> IdAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking()
            .Where(row => row.ClientId == EditorRecoveryView.Client).Select(row => row.Id!)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Hashes native metadata and policy; negative UI outcomes must not hide an application mutation or audit.</summary>
    internal static async Task<EditorRecoveryStamp> ReadAsync(BrowserHttpsFixture environment, string id)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == id, token);
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == id, token);
        var audit = await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == ApplicationTarget, token);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            native.ClientId,
            native.DisplayName,
            native.ClientSecret,
            native.RedirectUris,
            native.Permissions,
            Policy = policy
        })));
        return new(hash, policy.Revision, audit);
    }

    /// <summary>Removes a real persisted permission, then restores only the preexisting grant values in the isolated test database.</summary>
    internal static async Task WithoutPermissionAsync(BrowserHttpsFixture environment, string permission, Func<Task> check)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var original = await database.RoleClaims.AsNoTracking().Where(row => row.ClaimValue == permission).ToArrayAsync(token);
        Assert.NotEmpty(original);
        Assert.Equal(original.Length, await database.RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(token));
        try
        {
            await check();
        }
        finally
        {
            foreach (var row in original)
            {
                row.Id = GeneratedIdentifier;
            }
            database.RoleClaims.AddRange(original);
            await database.SaveChangesAsync(token);
        }
    }
}

/// <summary>Contains no native hash, credentials, identities or raw audit content suitable for accidental diagnostics.</summary>
internal sealed record EditorRecoveryStamp(string Digest, long Revision, int AuditCount);
