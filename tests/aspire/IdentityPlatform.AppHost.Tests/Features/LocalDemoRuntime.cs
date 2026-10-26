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
    private const string StartFailure = "Local demo initializer could not start.";
    private const int OperationSeconds = 90;
    private readonly IDistributedApplicationTestingBuilder builder;
    private readonly DistributedApplication application;
    private readonly string initializer;
    internal const string Username = "synthetic-demo-admin";
    internal string Password { get; } = PasswordPrefix + Guid.NewGuid().ToString(GuidFormat);

    /// <summary>Retains graph ownership until all browser/runtime consumers have completed.</summary>
    private LocalDemoRuntime(IDistributedApplicationTestingBuilder builder, DistributedApplication application, string run)
    {
        this.builder = builder;
        this.application = application;
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
            return new(builder, app, run);
        }
        catch
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
            await builder.DisposeAsync();
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
        await ExecuteAsync([Dotnet, Migrator, OperationArgument, BootstrapOperation], input, token);
        await ExecuteAsync([RegistrationScript], string.Empty, token);
    }

    /// <summary>Closes UTF-8 stdin before waiting; never logs bootstrap, environment, native errors or secret response bodies.</summary>
    private async Task ExecuteAsync(string[] arguments, string input, CancellationToken token)
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
        foreach (var argument in new[] { ExecArgument, StdinArgument, initializer }.Concat(arguments))
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
    }
}
