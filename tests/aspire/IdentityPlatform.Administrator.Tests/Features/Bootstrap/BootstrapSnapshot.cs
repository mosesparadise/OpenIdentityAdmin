using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Ordered current and historical rollback evidence for bootstrap scenarios.</summary>
internal static class BootstrapSnapshot
{
    /// <summary>Captures current rollback state, or explicitly projects historical fields for the pre-upgrade preservation witness.</summary>
    internal static async Task<string> SnapshotAsync(BootstrapDatabase lease, bool includeBootstrap = true)
    {
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var state = new List<object>
        {
            await database.Users.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Roles.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserRoles.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.RoleId).ToListAsync(Token),
            await database.RoleClaims.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserClaims.AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.UserLogins.AsNoTracking().OrderBy(value => value.LoginProvider).ThenBy(value => value.ProviderKey).ToListAsync(Token),
            await database.UserTokens.AsNoTracking().OrderBy(value => value.UserId).ThenBy(value => value.LoginProvider).ThenBy(value => value.Name).ToListAsync(Token),
            await database.Set<IdentityUserPasskey<string>>().AsNoTracking().OrderBy(value => value.CredentialId).ToListAsync(Token),
            await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<LoginBrowserBudget>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<PilotManifest>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            includeBootstrap ? await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(Token) :
                await HistoricalSecurityAudit.ReadAsync(database, Token)
        };
        if (includeBootstrap)
        {
            state.AddRange([
            await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(value => value.UserId).ToListAsync(Token),
            await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token),
            await database.Set<AdministratorGovernance>().AsNoTracking().OrderBy(value => value.Id).ToListAsync(Token)
            ]);
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }
}
