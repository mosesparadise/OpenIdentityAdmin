using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AppHost.Tests.Features;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed partial class IssuerEnvironment
{
    private readonly Dictionary<string, IssuerNode> nodes = new(StringComparer.Ordinal);
    private sealed record IssuerNode(ProcessGeneration Child, ProcessGeneration[] Tree, int Port);

    public static async Task AddSecondaryAsync(IDistributedApplicationTestingBuilder builder, CancellationToken cancellationToken)
    {
        var auth = builder.CreateResourceBuilder<ProjectResource>("auth");
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, auth.Resource, values, cancellationToken);
        foreach (var callback in auth.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }
        var connection = Assert.IsType<ReferenceExpression>(values["ConnectionStrings__platform"]);
        Assert.Contains("platform_runtime", connection.Format, StringComparison.Ordinal);
        var secondary = builder.AddProject<Projects.IdentityPlatform_AuthServer>("auth-secondary")
            .WithHttpsEndpoint(name: "https")
            .WaitForCompletion(builder.CreateResourceBuilder<ProjectResource>("migrator"))
            .WaitForCompletion(builder.CreateResourceBuilder<ProjectResource>("pilot-bootstrap"))
            .WithHttpHealthCheck("/health", endpointName: "https");
        foreach (var pair in values.Where(pair => pair.Key is "DOTNET_ENVIRONMENT" or "Database__Provider" or
            "ConnectionStrings__platform" or "Pilot__Enabled" or "Identity__Issuer" ||
            pair.Key.StartsWith("Identity__Protection__", StringComparison.Ordinal)))
        {
            if (pair.Key.EndsWith("__Password", StringComparison.Ordinal))
            {
                Assert.True(Assert.IsType<ParameterResource>(pair.Value).Secret, "Protection passwords must retain secret parameter references.");
            }
            secondary.WithEnvironment(context => context.EnvironmentVariables[pair.Key] = pair.Value);
        }
        foreach (var node in new[] { auth, secondary })
        {
            var endpoint = Assert.Single(node.Resource.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal("https", endpoint.UriScheme);
            endpoint.TargetHost = "127.0.0.1";
        }
    }

    public async Task ReadyNodesAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        await ReadyNodeAsync("auth", deadline.Token);
        await ReadyNodeAsync("auth-secondary", deadline.Token);
        Assert.True(nodes["auth"].Child != nodes["auth-secondary"].Child && nodes["auth"].Port != nodes["auth-secondary"].Port,
            "Distribution requires distinct actual processes and listeners.");
    }

    private async Task ReadyNodeAsync(string name, CancellationToken cancellationToken)
    {
        var state = await Application.ResourceNotifications.WaitForResourceAsync(name, change =>
        {
            if (change.Snapshot.State?.Text != KnownResourceStates.Running ||
                change.Snapshot.Properties.FirstOrDefault(value => value.Name == "executable.pid")?.Value is not { } raw)
            {
                return false;
            }
            var pid = Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
            return ProcessTreeEvidence.Read(pid) is { State: not 'Z' and not 'X' } current &&
                (!nodes.TryGetValue(name, out var prior) || !prior.Tree.Contains(current.Generation));
        }, cancellationToken);
        var id = Convert.ToInt32(state.Snapshot.Properties.Single(value => value.Name == "executable.pid").Value, System.Globalization.CultureInfo.InvariantCulture);
        using var process = Process.GetProcessById(id);
        Assert.False(process.HasExited, "Issuer launcher is not running.");
        await Application.ResourceNotifications.WaitForResourceAsync(name, change =>
            change.Snapshot.State?.Text == KnownResourceStates.Running && !change.Snapshot.HealthReports.IsEmpty &&
            change.Snapshot.HealthReports.All(report => LifecycleEvidence.HasFreshHealth(process.StartTime, report.LastRunAt, report.Status == HealthStatus.Healthy)), cancellationToken);
        var tree = ProcessTreeEvidence.Capture(id);
        var child = ProcessTreeEvidence.RequireExecutable(tree, "IdentityPlatform.AuthServer");
        var current = new IssuerNode(child, tree, ProcessListener.Capture(child));
        if (nodes.TryGetValue(name, out var old))
        {
            Assert.True(old.Child != child && old.Tree.All(ProcessTreeEvidence.Terminated), "Node start reused a live prior generation.");
        }
        nodes[name] = current;
        CaptureLifecycle();
        using var http = NodeClient(name);
        using var response = await http.GetAsync(new Uri(Issuer, "/health"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public HttpClient NodeClient(string name, CookieContainer? cookies = null, bool autoRedirect = false)
    {
        Assert.True(nodes.TryGetValue(name, out var node), "Node has no verified process-owned listener.");
        Assert.False(ProcessTreeEvidence.Terminated(node.Child), "Cannot create a transport for a stale node generation.");
        Assert.Equal(node.Port, ProcessListener.Capture(node.Child));
        return new HttpClient(new NodeTransport(Issuer, RelyingParty.BaseUri, node.Port, cookies ?? new CookieContainer(), autoRedirect, () =>
        {
            Assert.False(ProcessTreeEvidence.Terminated(node.Child), "Transport belongs to a stale node generation.");
            Assert.Equal(node.Port, ProcessListener.Capture(node.Child));
        }))
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
    }

    public async Task RequireStoppedSocketAsync(string name)
    {
        Assert.True(nodes[name].Tree.All(ProcessTreeEvidence.Terminated));
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var failure = await Assert.ThrowsAsync<SocketException>(async () =>
            await socket.ConnectAsync(IPAddress.Loopback, nodes[name].Port, TestContext.Current.CancellationToken));
        Assert.Equal(SocketError.ConnectionRefused, failure.SocketErrorCode);
    }

    public async Task StopNodeAsync(string name, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var stage = "StopNodeAsync";
        try
        {
            stage = "stop.capture";
            CaptureLifecycle();
            stage = "stop.initial-invariant";
            var invariant = DistributionInvariant();
            var node = nodes[name];
            var resource = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(value => value.Name == name);
            stage = "stop.command";
            Assert.True((await Application.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(resource, KnownResourceCommands.StopCommand, deadline.Token)).Success, "Node stop command failed.");
            stage = "stop.terminal-state";
            await Application.ResourceNotifications.WaitForResourceAsync(name, change =>
                change.Snapshot.State?.Text == KnownResourceStates.Exited || change.Snapshot.State?.Text == KnownResourceStates.Finished, deadline.Token);
            stage = "stop.process-exit";
            await ProcessTreeEvidence.WaitForTerminationAsync(node.Tree, deadline.Token);
            stage = "stop.retained-invariant";
            Assert.True(invariant == DistributionInvariant(), "Stopping a node changed retained issuer resources.");
            stage = "stop.peer";
            RequireOtherNode(name);
            stage = "stop.final-capture";
            CaptureLifecycle();
        }
        catch (Exception failure)
        {
            Invalidate(stage, failure);
            throw;
        }
    }

    public async Task StartNodeAsync(string name, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var stage = "StartNodeAsync";
        try
        {
            stage = "start.initial-invariant";
            var invariant = DistributionInvariant();
            var resource = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(value => value.Name == name);
            stage = "start.command";
            Assert.True((await Application.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(resource, KnownResourceCommands.StartCommand, deadline.Token)).Success, "Node start command failed.");
            stage = "start.ready";
            await ReadyNodeAsync(name, deadline.Token);
            stage = "start.retained-invariant";
            Assert.True(invariant == DistributionInvariant(), "Starting a node changed retained issuer resources.");
            stage = "start.peer";
            RequireOtherNode(name);
        }
        catch (Exception failure)
        {
            Invalidate(stage, failure);
            throw;
        }
        finally
        {
            try
            {
                CaptureLifecycle();
            }
            catch (Exception failure) { Invalidate("start.final-capture", failure); }
        }
    }
    private void RequireOtherNode(string name)
    {
        var other = nodes[DistributionEvidence.Other(name)];
        Assert.False(ProcessTreeEvidence.Terminated(other.Child), "Node operation replaced or stopped its peer.");
        Assert.Equal(other.Port, ProcessListener.Capture(other.Child));
    }

    private string DistributionInvariant()
    {
        Assert.True(Application.GetEndpoint("auth", "https") == Issuer, "Canonical issuer changed.");
        var oneShots = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources
            .Where(value => value.Name is "migrator" or "pilot-bootstrap").OrderBy(value => value.Name).Select(value =>
            {
                Assert.True(Application.ResourceNotifications.TryGetCurrentState(value.Name, out var state));
                return new
                {
                    value.Name,
                    state!.Snapshot.StartTimeStamp,
                    state.Snapshot.StopTimeStamp,
                    state.Snapshot.ExitCode
                };
            }).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Issuer = Issuer.AbsoluteUri,
            RelyingParty.Callback,
            Containers = LifecycleEvidence.Capture(Application).Containers.Order(StringComparer.Ordinal).ToArray(),
            OneShots = oneShots,
            Protection = ProtectionSnapshot().OrderBy(value => value.Key, StringComparer.Ordinal).ToArray()
        });
    }

}
