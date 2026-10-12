using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IdentityPlatform.Migrator.Features.Database.Upgrade;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

public sealed class BootstrapProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly TcpListener occupied = new(IPAddress.Loopback, 0);
    private readonly Task<string> output;
    private readonly Task<string> error;

    public BootstrapProcess(string provider, string connection, string operation = "initialize-administrator", string? environment = null, string? sessionConnection = null, bool provision = false, bool terminal = false)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(new Projects.IdentityPlatform_Migrator().ProjectPath);
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--configuration");
        start.ArgumentList.Add(typeof(DatabaseUpgrade).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration);
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--Operation");
        start.ArgumentList.Add(operation);
        start.Environment["DOTNET_ENVIRONMENT"] = environment ?? "Production";
        start.Environment["Database__Provider"] = provider;
        start.Environment["ConnectionStrings__platform"] = connection;
        start.Environment.Remove("ConnectionStrings__adminsessions");
        if (sessionConnection is not null)
        {
            start.Environment["ConnectionStrings__adminsessions"] = sessionConnection;
        }
        start.Environment.Remove("LocalDevelopment__ProvisionRuntimeRoles");
        if (provision)
        {
            start.Environment["LocalDevelopment__ProvisionRuntimeRoles"] = "true";
        }
        occupied.Start();
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:" + ((IPEndPoint)occupied.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (terminal)
        {
            // util-linux script allocates a real PTY; no credentials enter its command line.
            var invocation = string.Join(' ', new[] { start.FileName }.Concat(start.ArgumentList).Select(value => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'"));
            start.FileName = "script";
            start.ArgumentList.Clear();
            start.ArgumentList.Add("--quiet");
            start.ArgumentList.Add("--return");
            start.ArgumentList.Add("--echo");
            start.ArgumentList.Add("never");
            start.ArgumentList.Add("--command");
            start.ArgumentList.Add(invocation);
            start.ArgumentList.Add("/dev/null");
        }
        process = Process.Start(start)!;
        output = process.StandardOutput.ReadToEndAsync();
        error = process.StandardError.ReadToEndAsync();
    }

    public async Task SendAsync(byte[] input, bool close = true)
    {
        await process.StandardInput.BaseStream.WriteAsync(input, TestContext.Current.CancellationToken);
        await process.StandardInput.BaseStream.FlushAsync(TestContext.Current.CancellationToken);
        if (close)
        {
            process.StandardInput.Close();
        }
    }

    public async Task<(int ExitCode, string Output)> CompleteAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(200));
        await process.WaitForExitAsync(deadline.Token);
        return (process.ExitCode, await output.WaitAsync(deadline.Token) + await error.WaitAsync(deadline.Token));
    }

    public void Terminate()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            Terminate();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cleanup.Token);
            _ = await output.WaitAsync(cleanup.Token);
            _ = await error.WaitAsync(cleanup.Token);
        }
        finally
        {
            process.Dispose();
            occupied.Stop();
        }
    }
}
