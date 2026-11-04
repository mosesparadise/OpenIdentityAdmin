using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapSnapshot;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Historical upgrade preservation with the existing explicit audit projection.</summary>
internal static class BootstrapUpgradeScenario
{
    private const string UpgradeOperation = "upgrade";
    private const string UpgradeDisclosedInput = "Upgrade disclosed unused stdin.";
    private const string ChangedHistoricalState = "Upgrade modified prior Identity, pilot or session data.";
    private const int PreviousMigrationOffset = 2;
    private const int PriorSessionExpiry = 2;
    private const string PriorPilotUsername = "prior.pilot";
    private const string PriorClaim = "prior-claim";
    private const string PreservedClaimValue = "preserved";
    private const string PriorTokenProvider = "prior-provider";
    private const string PriorTokenName = "prior-token";
    private const string PriorTokenValue = "synthetic-token";
    private const string PriorClient = "prior-pilot";
    private const string PriorScope = "prior-scope";
    private const string PriorRedirect = "https://localhost/prior-callback";
    private const int PriorSessionIdentifierLength = 43;

    /// <summary>Runs the real upgrade and proves the complete historical state and unconsumed singleton survive.</summary>
    internal static async Task RunAsync(BootstrapDatabase lease, string provider)
    {
        await PrepareHistoricalStateAsync(lease);
        var priorState = await SnapshotAsync(lease, includeBootstrap: false);
        await using (var process = new BootstrapProcess(provider, lease.AdministratorConnection, UpgradeOperation, sessionConnection: lease.AdministratorConnection))
        {
            await process.SendAsync(lease.Input());
            var result = await process.CompleteAsync();
            Assert.Equal(0, result.ExitCode);
            Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal), UpgradeDisclosedInput);
        }
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Single(await database.Users.ToListAsync(Token));
            Assert.False((await database.Set<AdministratorGovernance>().SingleAsync(Token)).BootstrapConsumed);
            Assert.True(priorState == await SnapshotAsync(lease, includeBootstrap: false), ChangedHistoricalState);
        }
    }

    /// <summary>Creates the exact prior-schema Identity, pilot, claim, token and session preservation witness.</summary>
    private static async Task PrepareHistoricalStateAsync(BootstrapDatabase lease)
    {
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var migrations = (await database.Database.GetAppliedMigrationsAsync(Token)).ToArray();
            await database.GetService<IMigrator>().MigrateAsync(migrations[^PreviousMigrationOffset], Token);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = new PlatformUser { UserName = PriorPilotUsername };
            Require(await users.CreateAsync(user, lease.Password));
            Require(await users.AddClaimAsync(user, new System.Security.Claims.Claim(PriorClaim, PreservedClaimValue)));
            Require(await users.SetAuthenticationTokenAsync(user, PriorTokenProvider, PriorTokenName, PriorTokenValue));
            var application = new OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication { ClientId = PriorClient };
            var scopeRow = new OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope { Name = PriorScope };
            database.AddRange(application, scopeRow);
            database.Add(new PilotManifest { UserId = user.Id, UserName = user.UserName, ApplicationId = application.Id!, ScopeId = scopeRow.Id!, RedirectUri = PriorRedirect });
            database.Add(new LocalIdentitySession { Id = new string('s', PriorSessionIdentifierLength), UserId = user.Id, SecurityStamp = user.SecurityStamp!, AuthenticatedAt = 1, ExpiresAt = PriorSessionExpiry });
            await database.SaveChangesAsync(Token);
        }
    }
}
