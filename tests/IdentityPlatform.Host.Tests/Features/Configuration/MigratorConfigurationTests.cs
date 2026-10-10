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
        start.Environment.Remove("LocalDevelopment__ProvisionRuntimeRoles");
        if (bootstrap is not null) start.Environment["LocalDevelopment__ProvisionRuntimeRoles"] = bootstrap;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var combined = await output + await error;
        Assert.DoesNotContain("synthetic-secret", combined);
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("Invalid migration configuration", combined);
    }
    [Theory]
    [InlineData("AuthServer")]
    [InlineData("Admin.Api")]
    [InlineData("Admin.Bff")]
    [InlineData("Worker")]
    public async Task Actual_runtime_host_rejects_invalid_configuration_without_exposing_it(string host)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, $"IdentityPlatform.{host}.dll"));
        start.Environment["Database__Provider"] = "synthetic-secret";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var combined = await output + await error;
        Assert.DoesNotContain("synthetic-secret", combined);
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("Invalid database configuration", combined);
    }
}
