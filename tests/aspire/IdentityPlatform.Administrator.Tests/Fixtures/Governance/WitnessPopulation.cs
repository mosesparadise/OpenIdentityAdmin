using System.Globalization;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Exercises the native witness race in a populated user table without adding governance candidates or altering SQL configuration.</summary>
internal static class WitnessPopulation
{
    private const int OrdinaryUsers = 512;
    private const string OrdinaryPrefix = "witness-background-";

    /// <summary>Creates one real witness and a fixed set of passwordless ordinary users before fresh command proof is prepared.</summary>
    internal static async Task<WitnessSecurity> CreateAsync(GovernanceScenario scenario)
    {
        var witness = await WitnessSecurity.CreateAsync(scenario);
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < OrdinaryUsers; index++)
        {
            token.ThrowIfCancellationRequested();
            var user = new PlatformUser { UserName = OrdinaryPrefix + index.ToString(CultureInfo.InvariantCulture), IsEnabled = false };
            Assert.True((await users.CreateAsync(user)).Succeeded);
        }
        await RequireOrdinaryAsync(services.GetRequiredService<PlatformDbContext>(), token);
        var expected = new[] { scenario.ActorId, witness.Id }.Order(StringComparer.Ordinal).ToArray();
        var plan = await services.GetRequiredService<GovernanceDiscovery>().DiscoverAsync(scenario.ActorId, null, token);
        Assert.Equal(expected, plan.Users);
        Assert.Equal(expected, (await scenario.ReadAsync()).UsableAdministrators.Order(StringComparer.Ordinal));
        return witness;
    }

    /// <summary>Requires the added rows to have no password, enabled access, membership or administrator state.</summary>
    private static async Task RequireOrdinaryAsync(PlatformDbContext database, CancellationToken token)
    {
        var ordinary = database.Users.Where(row => row.UserName != null && row.UserName.StartsWith(OrdinaryPrefix));
        Assert.Equal(OrdinaryUsers, await ordinary.CountAsync(token));
        Assert.False(await ordinary.AnyAsync(row => row.IsEnabled || row.TwoFactorEnabled || row.PasswordHash != null, token));
        var ids = await ordinary.Select(row => row.Id).ToArrayAsync(token);
        Assert.False(await database.UserRoles.AnyAsync(row => ids.Contains(row.UserId), token));
        Assert.False(await database.Set<AdministratorEnrollment>().AnyAsync(row => ids.Contains(row.UserId), token));
        Assert.False(await database.Set<AdministratorAuthentication>().AnyAsync(row => ids.Contains(row.UserId), token));
        Assert.False(await database.Set<AdministratorFactor>().AnyAsync(row => ids.Contains(row.UserId), token));
        Assert.False(await database.Set<AdministratorSession>().AnyAsync(row => ids.Contains(row.UserId), token));
    }
}
