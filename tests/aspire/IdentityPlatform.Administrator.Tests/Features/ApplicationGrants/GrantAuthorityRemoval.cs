using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Prepares one real self-bundle removal with a surviving administrator and fresh genuine password/factor proof on an independent issuer root.</summary>
internal sealed class GrantAuthorityRemoval(FactorServices root, GovernanceScenario account, GovernanceRequest request, string code, Guid[] audits) : IAsyncDisposable
{
    private const string Removed = "AdministratorBundleRemoved";
    private const string Accepted = "Accepted";

    /// <summary>Warms production governance services and prepares immutable versions and proof before any contender holds the shared lock.</summary>
    internal static async Task<GrantAuthorityRemoval> PrepareAsync(GrantTestContext test, string provider, GrantAuthorityRaceProbe probe)
    {
        var account = test.Fixture.Scenario.Account;
        var request = await GovernanceCommands.PrepareAsync(account, GovernanceCommands.Remove);
        var code = await account.UnusedCodeAsync();
        var root = account.Journey.CreatePeer(provider, services =>
        {
            services.AddAdministratorGovernance();
            services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(probe));
        });
        try
        {
            await using var scope = root.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            _ = await services.GetRequiredService<GovernanceService>().ReadAsync(account.ActorId, test.Token);
            var audits = await services.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
                .AsNoTracking().Select(row => row.Id).ToArrayAsync(test.Token);
            return new(root, account, request, code, audits);
        }
        catch { await root.DisposeAsync(); throw; }
    }

    /// <summary>Executes the prepared command once with actual product authorization, native proof consumption, audit and commit.</summary>
    internal async Task<bool> ExecuteAsync()
    {
        await using var scope = root.Services.CreateAsyncScope();
        Assert.Equal(GovernanceOutcome.Accepted, await scope.ServiceProvider.GetRequiredService<GovernanceService>()
            .ExecuteAsync(request, account.Session, account.Password, code, TestContext.Current.CancellationToken));
        return true;
    }

    /// <summary>Independently requires committed permission loss and precisely one matching governance audit, separate from legitimate worker audit events.</summary>
    internal async Task RequireAsync()
    {
        await using var scope = root.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await database.UserRoles.AsNoTracking().AnyAsync(row => row.UserId == account.ActorId, token));
        Assert.True(await database.Users.AsNoTracking().Where(row => row.Id == account.ActorId).Select(row => row.IsEnabled).SingleAsync(token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().AsNoTracking()
            .Where(row => !audits.Contains(row.Id) && row.Event == Removed).ToArrayAsync(token));
        Assert.Equal(Accepted, audit.Outcome);
        Assert.Equal(account.ActorId, audit.ActorId);
        Assert.Equal(account.ActorId, audit.TargetId);
    }

    /// <summary>Releases the independent issuer only after both native contenders and all detached verification have completed.</summary>
    public ValueTask DisposeAsync() => root.DisposeAsync();
}
