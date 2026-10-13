using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Checks original native DbContext/transaction ownership using real independent transactions and one deliberately shared token.</summary>
internal static class GovernanceManagerOwnership
{
    private const int OperationSeconds = 5;
    private const string Specialist = "IdentityAdministrator";

    /// <summary>Proves that matching live tokens cannot authorize a manager owned by the other real DbContext.</summary>
    internal static async Task RequireForeignAsync(GovernanceScenario scenario, GovernanceCancellationEvidence evidence)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        await using var first = scenario.CreateScope();
        await using var second = scenario.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var b = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var aTransaction = await a.Database.BeginTransactionAsync(lifetime.Token);
        await using var bTransaction = await b.Database.BeginTransactionAsync(lifetime.Token);
        using var aLease = first.ServiceProvider.GetRequiredService<NativeOperation>().Enter(lifetime.Token);
        using var bLease = second.ServiceProvider.GetRequiredService<NativeOperation>().Enter(lifetime.Token);
        var roles = Assert.IsType<GovernanceRoleManager>(second.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var users = Assert.IsType<AdministratorUserManager>(second.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>());
        evidence.Arm(b, string.Empty, false, () => false, token: lifetime.Token);
        roles.RequireOperation(b, lifetime.Token);
        users.RequireOperation(b, lifetime.Token);
        Assert.Throws<FactorUnavailableException>(() => roles.RequireOperation(a, lifetime.Token));
        Assert.Throws<FactorUnavailableException>(() => users.RequireOperation(a, lifetime.Token));
        Assert.Equal(0, evidence.Commands);
        Assert.False(lifetime.IsCancellationRequested);
    }

    /// <summary>Ends the actual transaction while the native lease remains alive, then rejects both guards and actual manager lookups before SQL dispatch.</summary>
    internal static async Task RequireEndedAsync(GovernanceScenario scenario, GovernanceCancellationEvidence evidence)
    {
        var roleId = await GovernanceCommands.RoleAsync(scenario, Specialist);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var operation = scope.ServiceProvider.GetRequiredService<NativeOperation>();
        var roles = Assert.IsType<GovernanceRoleManager>(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var users = Assert.IsType<AdministratorUserManager>(scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>());
        await using var transaction = await database.Database.BeginTransactionAsync(lifetime.Token);
        using var lease = operation.Enter(lifetime.Token);
        roles.RequireOperation(database, lifetime.Token);
        users.RequireOperation(database, lifetime.Token);
        await transaction.DisposeAsync();
        evidence.Arm(database, string.Empty, false, () => false, token: lifetime.Token);
        Assert.Throws<FactorUnavailableException>(() => roles.RequireOperation(database, lifetime.Token));
        Assert.Throws<FactorUnavailableException>(() => users.RequireOperation(database, lifetime.Token));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => roles.FindByIdAsync(roleId));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.FindByIdAsync(scenario.ActorId));
        lease.Dispose();
        Assert.Throws<FactorUnavailableException>(() => roles.RequireOperation(database, lifetime.Token));
        Assert.Throws<FactorUnavailableException>(() => users.RequireOperation(database, lifetime.Token));
        Assert.Equal(0, evidence.Commands);
        Assert.False(lifetime.IsCancellationRequested);
    }
}
