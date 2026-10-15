using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class BootstrapAcceptanceTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Bootstrap_executable_completes_without_HTTP_and_preserves_state_on_repeat_and_conflict(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var address = (IPEndPoint)occupied.LocalEndpoint;
        var cancellationToken = TestContext.Current.CancellationToken;
        await AssertEmptyAsync(lease, cancellationToken);
        await RunBootstrapAsync(provider, lease, material, address.Port, conflict: false, expectedExit: 0);
        await AssertCoherentAsync(lease, created: 1, noOps: 0, cancellationToken);
        var created = await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true);
        await RunBootstrapAsync(provider, lease, material, address.Port, conflict: false, expectedExit: 0);
        await AssertCoherentAsync(lease, created: 1, noOps: 1, cancellationToken);
        Assert.True(created == await SnapshotAsync(lease, cancellationToken, createdAuditOnly: true), "Repeated command changed owned state.");
        var before = await SnapshotAsync(lease, cancellationToken);
        await RunBootstrapAsync(provider, lease, material, address.Port, conflict: true, expectedExit: 2);
        Assert.True(before == await SnapshotAsync(lease, cancellationToken), "Rejected command changed persisted state.");
    }

    private static async Task RunBootstrapAsync(string provider, BootstrapDatabase lease, IssuerMaterialFixture material,
        int occupiedPort, bool conflict, int expectedExit)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Run the production output: the test output has Aspire's distinct SQL client dependency graph.
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(new Projects.IdentityPlatform_AuthServer().ProjectPath);
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--configuration");
        start.ArgumentList.Add(typeof(IssuerCredentials).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration);
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--initialize-pilot");
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["Database__Provider"] = provider;
        start.Environment["ConnectionStrings__platform"] = lease.RuntimeConnection;
        start.Environment["Pilot__Enabled"] = "true";
        start.Environment["Pilot__UserName"] = lease.Username;
        start.Environment["Pilot__Password"] = lease.Password;
        start.Environment["Pilot__ClientSecret"] = lease.ClientSecret;
        start.Environment["Pilot__RedirectUri"] = conflict ? "https://localhost/conflicting-callback" : lease.Callback;
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:" + occupiedPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        foreach (var entry in material.Settings)
        {
            start.Environment[entry.Key.Replace(":", "__")] = entry.Value!;
        }
        using var process = Process.Start(start)!;
        // Always drain streams, including after independent cleanup on cancellation.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(75));
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
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token);
            await Task.WhenAll(output, error).WaitAsync(cleanup.Token);
        }
        var combined = await output + await error;
        Assert.Equal(expectedExit, process.ExitCode);
        Assert.False(combined.Contains("Now listening on:", StringComparison.Ordinal), "Bootstrap must not start an HTTP listener.");
        Assert.True(combined.Contains(expectedExit == 0 ? "First-party pilot initialization verified."
            : "First-party pilot initialization failed. Existing state was not repaired or re-enabled.", StringComparison.Ordinal),
            "Bootstrap must emit the expected fixed outcome message.");
        Assert.False(combined.Contains(lease.Password, StringComparison.Ordinal) || combined.Contains(lease.ClientSecret, StringComparison.Ordinal)
            || combined.Contains(material.Root, StringComparison.Ordinal), "Command output disclosed private material.");
        foreach (var secret in material.Settings.Where(value => value.Key.EndsWith(":Password", StringComparison.Ordinal)))
        {
            Assert.False(combined.Contains(secret.Value!, StringComparison.Ordinal), "Command output disclosed a certificate password.");
        }
    }
}
