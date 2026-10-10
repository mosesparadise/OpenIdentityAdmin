using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public static class IssuerLogEvidence
{
    public static async Task AssertSafeAsync(IssuerEnvironment environment, string incident, params string[] secrets)
    {
        var logs = environment.Application.Services.GetRequiredService<ResourceLoggerService>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var resource = environment.Application.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<ProjectResource>().Single(value => value.Name == "auth");
        var marker = $"Pilot request failed. Incident {incident}.";
        while (true)
        {
            var lines = new List<string>();
            await foreach (var batch in logs.GetAllAsync(resource).WithCancellation(deadline.Token))
            {
                lines.AddRange(batch.Select(line => line.Content));
            }
            var captured = string.Join('\n', lines);
            SafeAssertions.ContainsNoSensitiveMaterial(captured, secrets);
            if (captured.Contains(marker, StringComparison.Ordinal))
            {
                return;
            }
            await Task.Delay(25, deadline.Token);
        }
    }
}
