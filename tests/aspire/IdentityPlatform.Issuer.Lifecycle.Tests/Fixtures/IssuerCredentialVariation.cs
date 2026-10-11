using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed partial class IssuerEnvironment
{
    public async Task WithSecondaryCredentialAsync(string kind, Func<Task> whileChanged)
    {
        Assert.True(kind is "encryption" or "wrapper" or "missing", "Unknown credential experiment.");
        var section = kind == "wrapper" ? "DataProtection:Current" : "Encryption";
        var path = credentials.Settings[$"Identity:Protection:{section}:Path"]!;
        var password = credentials.Settings[$"Identity:Protection:{section}:Password"]!;
        var retained = path + ".retained";
        var original = ProtectionSnapshot();
        await StopNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        var moved = false;
        try
        {
            File.Move(path, retained);
            moved = true;
            if (kind != "missing")
            {
                using var rsa = RSA.Create(3072);
                var request = new CertificateRequest("CN=synthetic-distribution-mismatch", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
                IssuerMaterialFixture.WritePrivate(path, certificate.Export(X509ContentType.Pfx, password));
                using var loaded = IssuerCredentials.Load(runtimeConfiguration, DateTimeOffset.UtcNow);
                Assert.Equal(certificate.Thumbprint, kind == "encryption" ? loaded.Encryption.Thumbprint : loaded.Wrapping.Thumbprint);
            }
            if (kind == "encryption")
            {
                await StartNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
            }
            else
            {
                await RejectSecondaryStartupAsync(path, password);
            }
            await whileChanged();
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                Assert.True(Application.ResourceNotifications.TryGetCurrentState("auth-secondary", out var state));
                if (state!.Snapshot.State?.Text == KnownResourceStates.Running)
                {
                    await StopNodeAsync("auth-secondary", restoration.Token);
                }
            }
            catch (Exception) { Invalidate(); }
            try
            {
                if (moved)
                {
                    File.Delete(path);
                    File.Move(retained, path);
                }
                await StartNodeAsync("auth-secondary", restoration.Token);
                var restored = ProtectionSnapshot();
                Assert.True(original.Count == restored.Count && original.All(value => restored.TryGetValue(value.Key, out var hash) && hash == value.Value),
                    "Credential experiment changed retained protection material.");
            }
            catch (Exception) { Invalidate(); }
        }
        Assert.False(Invalidated, "Credential experiment could not restore the original two-node topology.");
    }

    private async Task RejectSecondaryStartupAsync(string path, string password)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var resource = Application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(value => value.Name == "auth-secondary");
        var launched = false;
        var becameHealthy = false;
        var startedAt = DateTimeOffset.UtcNow;
        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var probe = ObserveReadinessAsync(probeCancellation.Token, Application.GetEndpoint("auth-secondary", "https"));
        var reachedReadiness = false;
        var observed = Application.ResourceNotifications.WaitForResourceAsync("auth-secondary", change =>
        {
            if (change.Snapshot.State?.Text == KnownResourceStates.Running && change.Snapshot.Properties.Any(value => value.Name == "executable.pid" && value.Value is not null))
            {
                launched = true;
                CaptureLifecycle();
            }
            becameHealthy |= change.Snapshot.HealthReports.Any(report => report.Status == HealthStatus.Healthy && report.LastRunAt > startedAt);
            return launched && (change.Snapshot.State?.Text == KnownResourceStates.Exited || change.Snapshot.State?.Text == KnownResourceStates.Finished);
        }, deadline.Token);
        try
        {
            Assert.True((await Application.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(resource, KnownResourceCommands.StartCommand, deadline.Token)).Success);
            var terminal = await observed;
            Assert.Equal(2, terminal.Snapshot.ExitCode);
            Assert.False(becameHealthy, "Unsuitable secondary material reached readiness.");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            try
            {
                using var response = await client.GetAsync(new Uri(Application.GetEndpoint("auth-secondary", "https"), "/health"), deadline.Token);
                Assert.False(response.IsSuccessStatusCode, "Failed secondary still serves readiness.");
            }
            catch (HttpRequestException) { /* Failed process has no listener. */ }
            catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { /* Inactive Aspire proxy timeout. */ }
            var logs = Application.Services.GetRequiredService<ResourceLoggerService>();
            var found = false;
            while (!found)
            {
                var lines = new List<string>();
                await foreach (var batch in logs.GetAllAsync(resource).WithCancellation(deadline.Token))
                {
                    lines.AddRange(batch.Select(line => line.Content));
                }
                var output = string.Join('\n', lines);
                SafeAssertions.ContainsNoSensitiveMaterial(output, Password, ClientSecret, path, password);
                found = lines.Any(line =>
                {
                    var separator = line.IndexOf(' ', StringComparison.Ordinal);
                    return separator > 0 && DateTimeOffset.TryParse(line.AsSpan(0, separator), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) &&
                        timestamp >= startedAt && line.EndsWith(IssuerProtectionException.SafeMessage, StringComparison.Ordinal);
                });
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
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { /* Drain the owned observer. */ }
            catch (Exception) { Invalidate(); }
            reachedReadiness = await probe;
            try
            {
                CaptureLifecycle();
            }
            catch (Exception) { Invalidate(); }
        }
        Assert.False(reachedReadiness, "Invalid secondary material briefly served HTTP readiness.");
    }
}
