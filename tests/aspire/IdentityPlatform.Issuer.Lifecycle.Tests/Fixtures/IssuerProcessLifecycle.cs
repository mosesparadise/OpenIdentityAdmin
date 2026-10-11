using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AppHost.Tests.Features;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Compiled only by the dedicated lifecycle project; no lifecycle endpoint enters the product.
public sealed partial class IssuerEnvironment
{
    public async Task RestartIssuerAsync(Func<CancellationToken, Task>? whileStopped = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        var commands = Application.Services.GetRequiredService<ResourceCommandService>();
        var auth = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(value => value.Name == "auth");
        var before = LifecycleEvidence.Capture(Application);
        var old = CaptureIssuerProcess();
        var originalTree = ProcessTreeEvidence.Capture(old.Id);
        var originalServer = ProcessTreeEvidence.RequireExecutable(originalTree, "IdentityPlatform.AuthServer");
        var oneShots = OneShotSnapshot();
        var material = ProtectionSnapshot();
        var issuer = Issuer;
        var callback = RelyingParty.Callback;
        CaptureLifecycle();
        var elapsed = Stopwatch.StartNew();
        try
        {
            Assert.True((await commands.ExecuteCommandAsync(auth, KnownResourceCommands.StopCommand, token)).Success,
                "Issuer stop command failed.");
            await Application.ResourceNotifications.WaitForResourceAsync("auth", change =>
                change.Snapshot.State?.Text == KnownResourceStates.Exited || change.Snapshot.State?.Text == KnownResourceStates.Finished, token);
            Assert.True(HasExited(old), "The original issuer process did not exit.");
            await ProcessTreeEvidence.WaitForTerminationAsync(originalTree, token);
            if (whileStopped is not null)
            {
                await whileStopped(token);
            }
            Assert.True((await commands.ExecuteCommandAsync(auth, KnownResourceCommands.StartCommand, token)).Success,
                "Issuer start command failed.");
            await Application.ResourceNotifications.WaitForResourceAsync("auth", change =>
                change.Snapshot.State?.Text == KnownResourceStates.Running &&
                change.Snapshot.Properties.Any(value => value.Name == "executable.pid" && value.Value is not null), token);
            CaptureLifecycle(); // Capture the new generation before readiness can fail.
            var current = CaptureIssuerProcess();
            LifecycleEvidence.RequireReplacement(old, current, HasExited(old));
            await Application.ResourceNotifications.WaitForResourceAsync("auth", change =>
                change.Snapshot.State?.Text == KnownResourceStates.Running && !change.Snapshot.HealthReports.IsEmpty &&
                change.Snapshot.HealthReports.All(report => LifecycleEvidence.HasFreshHealth(current.Started, report.LastRunAt, report.Status == HealthStatus.Healthy)), token);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var ready = await http.GetAsync(new Uri(issuer, "/health"), token);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            var replacementTree = ProcessTreeEvidence.Capture(current.Id);
            var replacementServer = ProcessTreeEvidence.RequireExecutable(replacementTree, "IdentityPlatform.AuthServer");
            Assert.True(replacementServer != originalServer, "The actual AuthServer child was not replaced.");
            CaptureLifecycle();
            Assert.True(Application.GetEndpoint("auth", "https") == issuer && RelyingParty.Callback == callback,
                "Restart changed the canonical issuer or registered callback.");
            var after = LifecycleEvidence.Capture(Application);
            Assert.True(before.Containers.ToHashSet(StringComparer.Ordinal).SetEquals(after.Containers),
                "Restart replaced a durable database resource.");
            foreach (var process in before.Processes.Where(value => value.Id != old.Id))
            {
                Assert.True(after.Processes.Contains(process), "Restart replaced an unrelated live application process.");
            }
            Assert.True(oneShots == OneShotSnapshot(), "Restart reran or changed a completed bootstrap/migration resource.");
            var retained = ProtectionSnapshot();
            Assert.True(material.Count == retained.Count && material.All(value => retained.TryGetValue(value.Key, out var hash) && hash == value.Value),
                "Restart changed the original credential files or populated protection ring.");
            TestContext.Current.TestOutputHelper?.WriteLine("Issuer process replacement verified in {0:F3}s.", elapsed.Elapsed.TotalSeconds);
        }
        catch
        {
            Invalidate();
            throw;
        }
        finally
        {
            try
            {
                CaptureLifecycle();
            }
            catch (Exception) { Invalidate(); }
        }
    }

    public async Task RejectUnrelatedWrapperAsync(CancellationToken cancellationToken)
    {
        var path = credentials.Settings["Identity:Protection:DataProtection:Current:Path"]!;
        var password = credentials.Settings["Identity:Protection:DataProtection:Current:Password"]!;
        var retained = path + ".retained";
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=synthetic-unrelated-wrapper", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
        using var replacement = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.Move(path, retained);
        try
        {
            IssuerMaterialFixture.WritePrivate(path, replacement.Export(X509ContentType.Pfx, password));
            using (var validReplacement = IssuerCredentials.Load(runtimeConfiguration, DateTimeOffset.UtcNow))
            {
                Assert.Equal(replacement.Thumbprint, validReplacement.Wrapping.Thumbprint);
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            var auth = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(value => value.Name == "auth");
            var commands = Application.Services.GetRequiredService<ResourceCommandService>();
            var launched = false;
            var observed = Application.ResourceNotifications.WaitForResourceAsync("auth", change =>
            {
                if (change.Snapshot.State?.Text == KnownResourceStates.Running &&
                    change.Snapshot.Properties.Any(value => value.Name == "executable.pid" && value.Value is not null))
                {
                    CaptureLifecycle();
                    launched = true;
                }
                return launched && (change.Snapshot.State?.Text == KnownResourceStates.Exited || change.Snapshot.State?.Text == KnownResourceStates.Finished);
            }, deadline.Token);
            using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var probe = ObserveReadinessAsync(probeCancellation.Token);
            var reachedReadiness = false;
            try
            {
                Assert.True((await commands.ExecuteCommandAsync(auth, KnownResourceCommands.StartCommand, deadline.Token)).Success,
                    "Negative-control start command failed before process launch.");
                var terminal = await observed;
                Assert.Equal(2, terminal.Snapshot.ExitCode);
                CaptureLifecycle();
                var logs = Application.Services.GetRequiredService<ResourceLoggerService>();
                var found = false;
                while (!found)
                {
                    var lines = new List<string>();
                    await foreach (var batch in logs.GetAllAsync(auth).WithCancellation(deadline.Token))
                    {
                        lines.AddRange(batch.Select(line => line.Content));
                    }
                    var captured = string.Join('\n', lines);
                    SafeAssertions.ContainsNoSensitiveMaterial(captured, Password, ClientSecret, password, path);
                    found = captured.Contains(IssuerProtectionException.SafeMessage, StringComparison.Ordinal);
                    if (!found)
                    {
                        await Task.Delay(25, deadline.Token);
                    }
                }
            }
            finally
            {
                await probeCancellation.CancelAsync();
                await deadline.CancelAsync();
                try
                {
                    await observed;
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested) { /* Drain the owned state observer. */ }
                catch (Exception) { Invalidate(); } // Preserve the primary start/observation failure.
                reachedReadiness = await probe;
            }
            Assert.False(reachedReadiness, "Invalid populated protection ring reached HTTP readiness.");
        }
        finally
        {
            File.Delete(path);
            File.Move(retained, path);
        }
    }

    private async Task<bool> ObserveReadinessAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var ready = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    CaptureLifecycle();
                    using var response = await http.GetAsync(new Uri(Issuer, "/health"), cancellationToken);
                    ready |= response.IsSuccessStatusCode;
                }
                catch (HttpRequestException) { /* Expected: no issuer listener. */ }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { /* Bounded probe timeout. */ }
                await Task.Delay(50, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* Drain the owned probe. */ }
        return ready;
    }

    private string OneShotSnapshot()
    {
        var snapshots = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ProjectResource>()
            .Where(value => value.Name != "auth").OrderBy(value => value.Name).Select(value =>
            {
                Assert.True(Application.ResourceNotifications.TryGetCurrentState(value.Name, out var state));
                return new
                {
                    value.Name,
                    state!.Snapshot.StartTimeStamp,
                    state.Snapshot.StopTimeStamp,
                    ProcessId = state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value
                };
            }).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(snapshots);
    }

    private Dictionary<string, string> ProtectionSnapshot() => Directory.GetFiles(credentials.Root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    private (int Id, DateTime Started) CaptureIssuerProcess()
    {
        Assert.True(Application.ResourceNotifications.TryGetCurrentState("auth", out var state), "Missing issuer process state.");
        var id = Convert.ToInt32(state!.Snapshot.Properties.Single(value => value.Name == "executable.pid").Value, System.Globalization.CultureInfo.InvariantCulture);
        using var process = Process.GetProcessById(id);
        Assert.False(process.HasExited, "The issuer process is not running.");
        return (id, process.StartTime);
    }

    private static bool HasExited((int Id, DateTime Started) identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.Id);
            return process.HasExited || process.StartTime != identity.Started;
        }
        catch (ArgumentException) { return true; }
    }
}
