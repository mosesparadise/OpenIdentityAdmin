using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Owns the actual disposable Aspire graph and invokes initialization solely through bounded Docker stdin.</summary>
internal sealed class LocalDemoRuntime : IAsyncDisposable
{
    private const string PasswordPrefix = "Aa1!";
    private const string ContainerPrefix = "oid-demo-";
    private const string NameSeparator = "-";
    private const string InitializerSuffix = "-initializer";
    private const string GuidFormat = "N";
    private const string DevelopmentArgument = "--environment=Development";
    private const string EnabledArgument = "--LocalDemo:Enabled=true";
    private const string RunArgument = "--LocalDemo:RunId=";
    private const string ProviderArgument = "--Database:Provider=";
    private const string IssuerResource = "auth";
    private const string ClientResource = "admin-bff";
    private const string InitializerResource = "initializer";
    private const string OperatorReference = "local-demo-test";
    private const string Dotnet = "dotnet";
    private const string Migrator = "/app/IdentityPlatform.Migrator.dll";
    private const string OperationArgument = "--Operation";
    private const string BootstrapOperation = "initialize-administrator";
    private const string RegistrationScript = "/app/register.sh";
    private const string Docker = "docker";
    private const string ExecArgument = "exec";
    private const string StdinArgument = "-i";
    private const string InspectArgument = "inspect";
    private const string FormatArgument = "--format";
    private const string PortsProjection = "{{json .NetworkSettings.Ports}}";
    private const string HostAddressProperty = "HostIp";
    private const string HostPortProperty = "HostPort";
    private const string LoopbackAddress = "127.0.0.1";
    private const string IssuerPort = "17443";
    private const string ClientPort = "17444";
    private const string TcpSuffix = "/tcp";
    private const string StartFailure = "Local demo initializer could not start.";
    private const int OperationSeconds = 90;
    private readonly IDistributedApplicationTestingBuilder builder;
    private readonly DistributedApplication application;
    private readonly string initializer;
    private readonly string run;
    private string[] volumes = [];
    internal const string Username = "synthetic-demo-admin";
    internal string Password { get; } = PasswordPrefix + Guid.NewGuid().ToString(GuidFormat);

    /// <summary>Retains graph ownership until all browser/runtime consumers have completed.</summary>
    private LocalDemoRuntime(IDistributedApplicationTestingBuilder builder, DistributedApplication application, string run)
    {
        this.builder = builder;
        this.application = application;
        this.run = run;
        initializer = ContainerPrefix + run + InitializerSuffix;
    }

    /// <summary>Starts the production container graph and waits for genuine schema and runtime readiness without changing its resources.</summary>
    internal static async Task<LocalDemoRuntime> StartAsync(string provider, CancellationToken token)
    {
        var run = Guid.NewGuid().ToString(GuidFormat);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [DevelopmentArgument, EnabledArgument, RunArgument + run, ProviderArgument + provider], token);
        DistributedApplication? app = null;
        try
        {
            app = await builder.BuildAsync(token);
            await app.StartAsync(token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(IssuerResource, token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(ClientResource, token);
            await app.ResourceNotifications.WaitForResourceAsync(InitializerResource, KnownResourceStates.Running, token);
            await RequireLoopbackAsync(run, token);
            var runtime = new LocalDemoRuntime(builder, app, run);
            runtime.volumes = await LocalDemoLifecycle.CaptureVolumesAsync(run, token);
            return runtime;
        }
        catch
        {
            try
            {
                if (app is not null)
                {
                    await app.DisposeAsync();
                }
            }
            finally { await builder.DisposeAsync(); }
            throw;
        }
    }

    /// <summary>Creates only a pending administrator and performs separate public registration and revision-one upgrade operations.</summary>
    internal async Task InitializeAsync(CancellationToken token)
    {
        var input = JsonSerializer.Serialize(new
        {
            operationId = Guid.NewGuid(),
            operatorReference = OperatorReference,
            userName = Username,
            password = Password
        });
        await ExecuteAsync([ExecArgument, StdinArgument, initializer, Dotnet, Migrator, OperationArgument, BootstrapOperation], input, token);
        await ExecuteAsync([ExecArgument, StdinArgument, initializer, RegistrationScript], string.Empty, token);
    }

    /// <summary>Inspects only actual published ports and rejects wildcard exposure without retaining Docker environment metadata.</summary>
    private static async Task RequireLoopbackAsync(string run, CancellationToken token)
    {
        foreach (var (role, port) in new[] { (IssuerResource, IssuerPort), (ClientResource, ClientPort) })
        {
            var name = ContainerPrefix + run + NameSeparator + role;
            var output = await ExecuteAsync([InspectArgument, FormatArgument, PortsProjection, name], string.Empty, token);
            using var ports = JsonDocument.Parse(output);
            var published = ports.RootElement.EnumerateObject().Where(value => value.Value.ValueKind != JsonValueKind.Null).ToArray();
            Assert.Equal(port + TcpSuffix, Assert.Single(published).Name);
            var binding = Assert.Single(published[0].Value.EnumerateArray());
            Assert.Equal(LoopbackAddress, binding.GetProperty(HostAddressProperty).GetString());
            Assert.Equal(port, binding.GetProperty(HostPortProperty).GetString());
        }
    }

    /// <summary>Closes UTF-8 stdin before waiting; never logs bootstrap, environment, native errors or secret response bodies.</summary>
    internal static async Task<string> ExecuteAsync(string[] arguments, string input, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        var start = new ProcessStartInfo(Docker)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException(StartFailure);
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(output, errors);
            Assert.Equal(0, process.ExitCode);
            return await output;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            try
            {
                await Task.WhenAll(output, errors);
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>Stops/removes the complete graph, including any interrupted remote initialization, before releasing the testing builder.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await application.DisposeAsync();
        }
        finally { await builder.DisposeAsync(); }
        await LocalDemoLifecycle.RequireRemovedAsync(run, volumes);
    }
}
