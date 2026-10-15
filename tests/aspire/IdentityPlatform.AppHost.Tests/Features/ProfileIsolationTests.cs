using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features;

public sealed class ProfileIsolationTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Cancellation_while_waiting_for_startup_disposes_started_resources(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}"], token);
        // Hold initialization explicitly so startup readiness cannot race cancellation.
        // The database still starts through the real provider profile.
        builder.CreateResourceBuilder<ProjectResource>("migrator").WithExplicitStart();
        LifecycleEvidence resources;
        await using (var app = await builder.BuildAsync(token))
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
            var starting = app.StartAsync(startup.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync("database", token);
            Assert.False(starting.IsCompleted);
            resources = LifecycleEvidence.Capture(app);
            startup.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        }
        await resources.AssertRemovedAsync();
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Concurrent_environments_keep_data_endpoints_and_teardown_separate(string providerName)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await using var firstBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={providerName}"], token);
        await using var first = await firstBuilder.BuildAsync(token);
        await first.StartAsync(token);
        await first.ResourceNotifications.WaitForResourceHealthyAsync("auth", token);
        await using var secondBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={providerName}"], token);
        await using var second = await secondBuilder.BuildAsync(token);
        await second.StartAsync(token);
        await second.ResourceNotifications.WaitForResourceHealthyAsync("auth", token);
        Assert.NotEqual(first.GetEndpoint("auth", "https"), second.GetEndpoint("auth", "https"));

        var provider = DatabaseProviderParser.Parse(providerName);
        await using (var firstDb = Database(await first.GetConnectionStringAsync("platform", token)))
        {
            await firstDb.Database.ExecuteSqlRawAsync("CREATE TABLE platform.isolation_probe (Id integer)", token);
        }
        await using (var secondDb = Database(await second.GetConnectionStringAsync("platform", token)))
        {
            var connection = secondDb.Database.GetDbConnection();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'isolation_probe'";
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture));
        }
        var firstResources = LifecycleEvidence.Capture(first);
        await first.StopAsync(token);
        await firstResources.AssertRemovedAsync();
        using var client = new HttpClient { BaseAddress = second.GetEndpoint("auth", "https") };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", token)).StatusCode);

        PlatformDbContext Database(string? connection)
        {
            Assert.False(string.IsNullOrEmpty(connection));
            var options = new DbContextOptionsBuilder<PlatformDbContext>();
            PersistenceRegistration.ConfigureDatabase(options, provider, connection!, "PlatformMigrations");
            return new(options.Options);
        }
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Invalid_service_configuration_is_observable_and_the_environment_disposes(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}"], token);
        var auth = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "auth");
        auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            context.EnvironmentVariables["Database__Provider"] = "invalid-provider"));
        LifecycleEvidence resources;
        await using (var app = await builder.BuildAsync(token))
        {
            await app.StartAsync(token);
            await app.ResourceNotifications.WaitForResourceAsync("auth", KnownResourceStates.Finished, token);
            Assert.True(app.ResourceNotifications.TryGetCurrentState("auth", out var state));
            Assert.Equal(2, state!.Snapshot.ExitCode);
            resources = LifecycleEvidence.Capture(app);
        }
        await resources.AssertRemovedAsync();
    }
}
