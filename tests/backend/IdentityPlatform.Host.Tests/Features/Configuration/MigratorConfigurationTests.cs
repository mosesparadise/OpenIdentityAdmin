using System.Diagnostics;
using IdentityPlatform.Migrator.Features.Database.Upgrade;

namespace IdentityPlatform.Host.Tests.Features.Configuration;

public sealed class MigratorConfigurationTests
{
    [Theory]
    [InlineData("synthetic-secret", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Development")]
    [InlineData("false", "Development")]
    [InlineData(null, "Development")]
    public async Task Invalid_configuration_returns_safe_nonzero_exit(string? bootstrap, string environment)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(DatabaseUpgrade).Assembly.Location);
        start.Environment["DOTNET_ENVIRONMENT"] = environment;
        start.Environment["Database__Provider"] = "PostgreSql";
        start.Environment["ConnectionStrings__platform"] = "";
        start.Environment["ConnectionStrings__adminsessions"] = "";
        start.Environment["LocalDevelopment__PlatformPassword"] = "";
        start.Environment["LocalDevelopment__SessionsPassword"] = "";
        if (bootstrap is null)
        {
            start.Environment.Remove("ConnectionStrings__platform");
            start.Environment.Remove("ConnectionStrings__adminsessions");
        }
        if (bootstrap == "true" && environment == "Development")
        {
            start.Environment.Remove("LocalDevelopment__PlatformPassword");
            start.Environment.Remove("LocalDevelopment__SessionsPassword");
        }
        start.Environment.Remove("LocalDevelopment__ProvisionRuntimeRoles");
        if (bootstrap is not null) start.Environment["LocalDevelopment__ProvisionRuntimeRoles"] = bootstrap;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var combined = await output + await error;
        Assert.DoesNotContain("synthetic-secret", combined);
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("Invalid migration configuration", combined);
    }

    public static IEnumerable<object[]> RuntimeConfigurationCases()
    {
        foreach (var host in new[] { "AuthServer", "Admin.Api", "Admin.Bff", "Worker" })
        {
            yield return [host, "synthetic-secret", null!];
            yield return [host, "PostgreSql", null!];
            yield return [host, "PostgreSql", "Password=synthetic-secret"];
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeConfigurationCases))]
    public async Task Actual_runtime_host_rejects_invalid_configuration_without_exposing_it(string host, string provider, string? connection)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, $"IdentityPlatform.{host}.dll"));
        start.Environment["Database__Provider"] = provider;
        start.Environment.Remove("ConnectionStrings__platform");
        start.Environment.Remove("ConnectionStrings__adminsessions");
        if (connection is not null)
            start.Environment[host == "Admin.Bff" ? "ConnectionStrings__adminsessions" : "ConnectionStrings__platform"] = connection;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var combined = await output + await error;
        Assert.DoesNotContain("synthetic-secret", combined);
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("Invalid database configuration", combined);
    }
}
