using System.Diagnostics;
using IdentityPlatform.Migrator.Features.Database.Upgrade;

namespace IdentityPlatform.Host.Tests.Features.Configuration;

/// <summary>Exercise safe configuration failures through the actual production entry points.</summary>
public sealed class MigratorConfigurationTests
{
    private const string DotnetHostPath = "DOTNET_HOST_PATH";
    private const string DotnetCommand = "dotnet";
    private const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
    private const string ProviderKey = "Database__Provider";
    private const string PlatformConnection = "ConnectionStrings__platform";
    private const string SessionsConnection = "ConnectionStrings__adminsessions";
    private const string PlatformPassword = "LocalDevelopment__PlatformPassword";
    private const string SessionsPassword = "LocalDevelopment__SessionsPassword";
    private const string ProvisionRuntimeRoles = "LocalDevelopment__ProvisionRuntimeRoles";
    private const string DevelopmentEnvironment = "Development";
    private const string EnabledFlag = "true";
    private const string PostgreSqlProvider = "PostgreSql";
    private const string BffHost = "Admin.Bff";
    private const string SyntheticSecret = "synthetic-secret";
    private const string InvalidConnection = "Password=synthetic-secret";
    private const string MigrationFailure = "Invalid migration configuration";
    private const string DatabaseFailure = "Invalid database configuration";
    private const string AssemblyPrefix = "IdentityPlatform.";
    private const string AssemblySuffix = ".dll";
    private const int ConfigurationFailureExitCode = 2;
    private static readonly TimeSpan ExitDeadline = TimeSpan.FromSeconds(10);
    private static readonly string[] RuntimeHosts = ["AuthServer", "Admin.Api", BffHost, "Worker"];

    /// <summary>Verify invalid migration settings produce a safe diagnostic and a nonzero exit.</summary>
    [Theory]
    [InlineData("synthetic-secret", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Development")]
    [InlineData("false", "Development")]
    [InlineData(null, "Development")]
    public async Task Given_InvalidMigratorConfiguration_When_ProcessStarts_Then_ExitsSafely(string? bootstrap, string environment)
    {
        var start = CreateStart(typeof(DatabaseUpgrade).Assembly.Location);
        ConfigureMigrator(start, bootstrap, environment);
        await RequireSafeFailureAsync(start, MigrationFailure);
    }

    /// <summary>Supply invalid provider, missing connection and incomplete connection cases for every runtime host.</summary>
    public static IEnumerable<object[]> RuntimeConfigurationCases()
    {
        foreach (var host in RuntimeHosts)
        {
            yield return [host, SyntheticSecret, null!];
            yield return [host, PostgreSqlProvider, null!];
            yield return [host, PostgreSqlProvider, InvalidConnection];
        }
    }

    /// <summary>Verify each real runtime host rejects invalid database settings without disclosure.</summary>
    [Theory]
    [MemberData(nameof(RuntimeConfigurationCases))]
    public async Task Given_InvalidRuntimeConfiguration_When_HostStarts_Then_ExitsWithoutDisclosingInput(string host, string provider, string? connection)
    {
        var start = CreateStart(Path.Combine(AppContext.BaseDirectory, AssemblyPrefix + host + AssemblySuffix));
        start.Environment[ProviderKey] = provider;
        start.Environment.Remove(PlatformConnection);
        start.Environment.Remove(SessionsConnection);
        if (connection is not null)
        {
            start.Environment[host == BffHost ? SessionsConnection : PlatformConnection] = connection;
        }
        await RequireSafeFailureAsync(start, DatabaseFailure);
    }

    /// <summary>Run the selected assembly with redirected streams and the current test runtime.</summary>
    private static ProcessStartInfo CreateStart(string assembly)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable(DotnetHostPath) ?? DotnetCommand)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(assembly);
        return start;
    }

    /// <summary>Preserve the missing/empty connection and bootstrap-password combinations used by the negative controls.</summary>
    private static void ConfigureMigrator(ProcessStartInfo start, string? bootstrap, string environment)
    {
        start.Environment[DotnetEnvironment] = environment;
        start.Environment[ProviderKey] = PostgreSqlProvider;
        start.Environment[PlatformConnection] = "";
        start.Environment[SessionsConnection] = "";
        start.Environment[PlatformPassword] = "";
        start.Environment[SessionsPassword] = "";
        if (bootstrap is null)
        {
            start.Environment.Remove(PlatformConnection);
            start.Environment.Remove(SessionsConnection);
        }
        if (bootstrap == EnabledFlag && environment == DevelopmentEnvironment)
        {
            start.Environment.Remove(PlatformPassword);
            start.Environment.Remove(SessionsPassword);
        }
        start.Environment.Remove(ProvisionRuntimeRoles);
        if (bootstrap is not null)
        {
            start.Environment[ProvisionRuntimeRoles] = bootstrap;
        }
    }

    /// <summary>Bound process exit, drain both streams and reject secret disclosure while retaining tree cleanup on timeout.</summary>
    private static async Task RequireSafeFailureAsync(ProcessStartInfo start, string expectedDiagnostic)
    {
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(ExitDeadline);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        var combined = await output + await error;
        Assert.DoesNotContain(SyntheticSecret, combined);
        Assert.Equal(ConfigurationFailureExitCode, process.ExitCode);
        Assert.Contains(expectedDiagnostic, combined);
    }
}
