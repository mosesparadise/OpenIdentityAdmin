using System.Text;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

public sealed partial class BootstrapTests
{
    private static readonly string[] Processes = ["lost-result", "old-schema", "runtime-only", "unrelated-data", "upgrade-only"];
    private static readonly string[] Inputs = ["invalid-utf8", "too-large", "missing-eof", "invalid-canary", "escaped-high", "escaped-low"];
    public static IEnumerable<TheoryDataRow<string, string>> ProcessCases => Cases(Processes);
    public static IEnumerable<TheoryDataRow<string, string>> InputCases => Cases(Inputs);

    [Theory]
    [MemberData(nameof(ProcessCases))]
    public async Task Process_restart_upgrade_and_privilege_boundaries_are_explicit(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        switch (scenario)
        {
            case "lost-result":
                await using (var first = new BootstrapProcess(provider, lease.RuntimeConnection))
                {
                    await first.SendAsync(lease.Input());
                    // Deliberately discard completion/output: the next invocation must reconcile persisted state.
                    _ = await first.CompleteAsync();
                }
                var before = await SnapshotAsync(lease);
                var again = await RunAsync(lease, provider, lease.Input());
                Assert.Equal(0, again.ExitCode);
                Assert.True(again.Output.Contains("already initialized", StringComparison.Ordinal), "Reconciliation outcome missing.");
                Assert.True(before == await SnapshotAsync(lease), "Ambiguous-result reconciliation changed state.");
                break;
            case "old-schema":
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    var migrations = (await database.Database.GetAppliedMigrationsAsync(Token)).ToArray();
                    await database.GetService<IMigrator>().MigrateAsync(migrations[^2], Token);
                }
                var old = await RunAsync(lease, provider, lease.Input());
                Assert.Equal(1, old.ExitCode);
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.ToListAsync(Token));
                }
                break;
            case "runtime-only":
                await using (var scope = lease.Runtime.CreateAsyncScope())
                {
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    Assert.NotNull(await Record.ExceptionAsync(() => database.Database.ExecuteSqlRawAsync("CREATE TABLE platform.unauthorized_probe (id int)", Token)));
                }
                Assert.Equal(0, (await RunAsync(lease, provider, lease.Input())).ExitCode);
                break;
            case "unrelated-data":
                string original;
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
                    var user = new PlatformUser { UserName = "unrelated.user" };
                    Require(await users.CreateAsync(user, lease.Password));
                    original = user.PasswordHash!;
                }
                Assert.Equal(0, (await RunAsync(lease, provider, lease.Input())).ExitCode);
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    var user = await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().FindByNameAsync("unrelated.user");
                    Assert.True(user is { IsEnabled: true } && user.PasswordHash == original, "Unrelated account was modified.");
                }
                break;
            case "upgrade-only":
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    var migrations = (await database.Database.GetAppliedMigrationsAsync(Token)).ToArray();
                    await database.GetService<IMigrator>().MigrateAsync(migrations[^2], Token);
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
                    var user = new PlatformUser { UserName = "prior.pilot" };
                    Require(await users.CreateAsync(user, lease.Password));
                    Require(await users.AddClaimAsync(user, new System.Security.Claims.Claim("prior-claim", "preserved")));
                    Require(await users.SetAuthenticationTokenAsync(user, "prior-provider", "prior-token", "synthetic-token"));
                    var application = new OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication { ClientId = "prior-pilot" };
                    var scopeRow = new OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope { Name = "prior-scope" };
                    database.AddRange(application, scopeRow);
                    database.Add(new PilotManifest { UserId = user.Id, UserName = user.UserName, ApplicationId = application.Id!, ScopeId = scopeRow.Id!, RedirectUri = "https://localhost/prior-callback" });
                    database.Add(new LocalIdentitySession { Id = new string('s', 43), UserId = user.Id, SecurityStamp = user.SecurityStamp!, AuthenticatedAt = 1, ExpiresAt = 2 });
                    await database.SaveChangesAsync(Token);
                }
                var priorState = await SnapshotAsync(lease, includeBootstrap: false);
                await using (var process = new BootstrapProcess(provider, lease.AdministratorConnection, "upgrade", sessionConnection: lease.AdministratorConnection))
                {
                    await process.SendAsync(lease.Input());
                    var result = await process.CompleteAsync();
                    Assert.Equal(0, result.ExitCode);
                    Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal), "Upgrade disclosed unused stdin.");
                }
                await using (var scope = lease.Administrator.CreateAsyncScope())
                {
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    Assert.Single(await database.Users.ToListAsync(Token));
                    Assert.False((await database.Set<AdministratorGovernance>().SingleAsync(Token)).BootstrapConsumed);
                    Assert.True(priorState == await SnapshotAsync(lease, includeBootstrap: false), "Upgrade modified prior Identity, pilot or session data.");
                }
                break;
        }
    }

    [Theory]
    [MemberData(nameof(InputCases))]
    public async Task Actual_pipe_input_is_bounded_and_never_disclosed(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        await using var process = new BootstrapProcess(provider, lease.RuntimeConnection);
        var input = scenario switch
        {
            "escaped-high" => Encoding.UTF8.GetBytes("{\"password\":\"\\uD800\"}"),
            "escaped-low" => Encoding.UTF8.GetBytes("{\"password\":\"\\uDC00\"}"),
            "invalid-utf8" => new byte[] { 0xFF, 0xFE },
            "too-large" => Encoding.UTF8.GetBytes(new string(' ', 4097)),
            "invalid-canary" => Encoding.UTF8.GetBytes("{\"password\":\"canary-secret-value\",\"unknown\":true}"),
            _ => lease.Input()
        };
        await process.SendAsync(input, close: scenario != "missing-eof");
        var result = await process.CompleteAsync();
        Assert.Equal(scenario == "missing-eof" ? 1 : 2, result.ExitCode);
        Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal) || result.Output.Contains("canary-secret-value", StringComparison.Ordinal), "Input appeared in process diagnostics.");
        Assert.True(before == await SnapshotAsync(lease), "Invalid or incomplete input wrote database state.");
    }
    private static readonly string[] Configuration = ["operation", "local-roles", "missing-connection", "terminal"];
    public static IEnumerable<TheoryDataRow<string, string>> ConfigurationCases => Cases(Configuration);

    [Theory]
    [MemberData(nameof(ConfigurationCases))]
    public async Task Process_rejects_unsafe_operation_configuration_before_any_write(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        await using var process = new BootstrapProcess(provider, scenario == "missing-connection" ? "" : lease.RuntimeConnection,
            operation: scenario == "operation" ? "unknown-operation" : "initialize-administrator",
            environment: scenario == "local-roles" ? "Development" : "Production",
            sessionConnection: scenario == "operation" ? lease.AdministratorConnection : null, provision: scenario == "local-roles", terminal: scenario == "terminal");
        // Otherwise valid input prevents malformed-input rejection from masking a missing guard.
        await process.SendAsync(lease.Input());
        var result = await process.CompleteAsync();
        Assert.Equal(2, result.ExitCode);
        var expected = scenario == "terminal" ? "Invalid administrator bootstrap input." : "Invalid migration configuration.";
        Assert.True(result.Output.Contains(expected, StringComparison.Ordinal), "Failure was not attributed to the expected command boundary.");
        Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal), "Rejected command or PTY echoed credentials.");
        Assert.True(before == await SnapshotAsync(lease), "Rejected operation configuration changed persisted state.");
    }

}
