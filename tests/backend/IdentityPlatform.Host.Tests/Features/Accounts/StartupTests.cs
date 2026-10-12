using System.Diagnostics;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Runs the production executable to prove invalid account settings exit safely before opening a listener.</summary>
public sealed class StartupTests
{
    private const string DotnetHost = "DOTNET_HOST_PATH";
    private const string Dotnet = "dotnet";
    private const string EnvironmentKey = "DOTNET_ENVIRONMENT";
    private const string Production = "Production";
    private const string ProviderKey = "Database__Provider";
    private const string Provider = "PostgreSql";
    private const string ConnectionKey = "ConnectionStrings__platform";
    private const string Connection = "Host=localhost;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    private const string EnabledKey = "Identity__AdministratorAccounts__Enabled";
    private const string UnknownKey = "Identity__AdministratorAccounts__Unknown";
    private const string UrlsKey = "ASPNETCORE_URLS";
    private const string Address = "http://127.0.0.1:0";
    private const string Sentinel = "synthetic-private-setting";
    private const string Unknown = "unknown";
    private const string Invalid = "invalid";
    private const int DeadlineSeconds = 20;
    private const int CleanupSeconds = 5;

    /// <summary>Exercises Program's classified error path rather than only testing the configuration parser.</summary>
    [Theory]
    [InlineData("invalid")]
    [InlineData("unknown")]
    [InlineData("unprotected")]
    public async Task Given_InvalidAdministratorSettings_When_ExecutableStarts_Then_ItExitsWithoutListeningOrLeaking(string scenario)
    {
        const string DisclosureFailure = "Startup diagnostics must not disclose configuration values.";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        using var process = Process.Start(StartInfo(scenario))!;
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            Assert.Equal(2, process.ExitCode);
            var diagnostic = await output + await errors;
            Assert.Contains("Invalid administrator account configuration. Explicit activation requires configured issuer protection.", diagnostic);
            Assert.False(diagnostic.Contains(Sentinel, StringComparison.Ordinal), DisclosureFailure);
            Assert.DoesNotContain("Now listening on:", diagnostic);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
                await process.WaitForExitAsync(cleanup.Token);
            }
        }
    }

    /// <summary>Builds an isolated synthetic invocation with no issuer protection or bootstrap operation.</summary>
    private static ProcessStartInfo StartInfo(string scenario)
    {
        const string IdentityPrefix = "Identity__";
        const string PilotPrefix = "Pilot__";
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable(DotnetHost) ?? Dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(IssuerCredentials).Assembly.Location);
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith(IdentityPrefix, StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith(PilotPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(key);
        }
        start.Environment[EnvironmentKey] = Production;
        start.Environment[ProviderKey] = Provider;
        start.Environment[ConnectionKey] = Connection;
        start.Environment[UrlsKey] = Address;
        start.Environment[EnabledKey] = scenario == Invalid ? Sentinel : bool.TrueString;
        if (scenario == Unknown)
        {
            start.Environment[EnabledKey] = bool.FalseString;
            start.Environment[UnknownKey] = Sentinel;
        }
        return start;
    }
}
