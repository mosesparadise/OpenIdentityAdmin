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

/// <summary>Native process lifecycle, schema and privilege boundary scenario workers.</summary>
internal static class BootstrapProcessScenarios
{
    private const string LostResultScenario = "lost-result";
    private const string OldSchemaScenario = "old-schema";
    private const string RuntimeOnlyScenario = "runtime-only";
    private const string UnrelatedDataScenario = "unrelated-data";
    private const string UpgradeOnlyScenario = "upgrade-only";
    private const string ReconciliationMarker = "already initialized";
    private const string MissingReconciliationOutcome = "Reconciliation outcome missing.";
    private const string ChangedAmbiguousResultState = "Ambiguous-result reconciliation changed state.";
    private const int PreviousMigrationOffset = 2;
    private const string UnauthorizedCreateTable = "CREATE TABLE platform.unauthorized_probe (id int)";
    private const string UnrelatedUser = "unrelated.user";
    private const string ChangedUnrelatedAccount = "Unrelated account was modified.";

    /// <summary>Executes one original process scenario without retry or fallback.</summary>
    internal static async Task RunScenarioAsync(BootstrapDatabase lease, string provider, string scenario)
    {
        switch (scenario)
        {
            case LostResultScenario:
                await LostResultAsync(lease, provider);
                break;
            case OldSchemaScenario:
                await OldSchemaAsync(lease, provider);
                break;
            case RuntimeOnlyScenario:
                await RuntimeOnlyAsync(lease, provider);
                break;
            case UnrelatedDataScenario:
                await UnrelatedDataAsync(lease, provider);
                break;
            case UpgradeOnlyScenario:
                await BootstrapUpgradeScenario.RunAsync(lease, provider);
                break;
        }
    }

    /// <summary>Discards the first command result, then requires exact persisted reconciliation.</summary>
    private static async Task LostResultAsync(BootstrapDatabase lease, string provider)
    {
        await using (var first = new BootstrapProcess(provider, lease.RuntimeConnection))
        {
            await first.SendAsync(lease.Input());
            // Deliberately discard completion/output: the next invocation must reconcile persisted state.
            _ = await first.CompleteAsync();
        }
        var before = await SnapshotAsync(lease);
        var again = await RunAsync(lease, provider, lease.Input());
        Assert.Equal(0, again.ExitCode);
        Assert.True(again.Output.Contains(ReconciliationMarker, StringComparison.Ordinal), MissingReconciliationOutcome);
        Assert.True(before == await SnapshotAsync(lease), ChangedAmbiguousResultState);
    }

    /// <summary>Downgrades one Platform migration and proves initialization refuses the old schema.</summary>
    private static async Task OldSchemaAsync(BootstrapDatabase lease, string provider)
    {
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var migrations = (await database.Database.GetAppliedMigrationsAsync(Token)).ToArray();
            await database.GetService<IMigrator>().MigrateAsync(migrations[^PreviousMigrationOffset], Token);
        }
        var old = await RunAsync(lease, provider, lease.Input());
        Assert.Equal(1, old.ExitCode);
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.ToListAsync(Token));
        }
    }

    /// <summary>Proves the runtime credential cannot create a table while ordinary initialization succeeds.</summary>
    private static async Task RuntimeOnlyAsync(BootstrapDatabase lease, string provider)
    {
        await using (var scope = lease.Runtime.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.NotNull(await Record.ExceptionAsync(() => database.Database.ExecuteSqlRawAsync(UnauthorizedCreateTable, Token)));
        }
        Assert.Equal(0, (await RunAsync(lease, provider, lease.Input())).ExitCode);
    }

    /// <summary>Checks the unrelated native account and original password hash survive initialization.</summary>
    private static async Task UnrelatedDataAsync(BootstrapDatabase lease, string provider)
    {
        string original;
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = new PlatformUser { UserName = UnrelatedUser };
            Require(await users.CreateAsync(user, lease.Password));
            original = user.PasswordHash!;
        }
        Assert.Equal(0, (await RunAsync(lease, provider, lease.Input())).ExitCode);
        await using (var scope = lease.Administrator.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().FindByNameAsync(UnrelatedUser);
            Assert.True(user is { IsEnabled: true } && user.PasswordHash == original, ChangedUnrelatedAccount);
        }
    }
}
