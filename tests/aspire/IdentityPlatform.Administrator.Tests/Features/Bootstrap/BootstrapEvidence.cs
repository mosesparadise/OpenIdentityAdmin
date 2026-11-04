using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Shared native bootstrap process and preparation assertions.</summary>
internal static class BootstrapEvidence
{
    private const string ManagerPreparationFailure = "Synthetic manager preparation failed.";
    private const string PasswordProperty = "password";
    private const string CredentialDisclosureFailure = "Bootstrap output disclosed a credential.";

    /// <summary>Uses the current xUnit operation cancellation token.</summary>
    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Enumerates providers and scenarios in the original order with the exact provider trait.</summary>
    internal static IEnumerable<TheoryDataRow<string, string>> Cases(IEnumerable<string> cases) =>
        from provider in Providers from scenario in cases select new TheoryDataRow<string, string>(provider, scenario).WithTrait(ProviderTrait, provider);

    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private const string ProviderTrait = "Provider";

    /// <summary>Requires successful native manager fixture preparation.</summary>
    internal static void Require(IdentityResult result) => Assert.True(result.Succeeded, ManagerPreparationFailure);

    /// <summary>Runs the real command once, joins completion and rejects disclosure of the submitted password.</summary>
    internal static async Task<(int ExitCode, string Output)> RunAsync(BootstrapDatabase lease, string provider, byte[] input)
    {
        await using var process = new BootstrapProcess(provider, lease.RuntimeConnection);
        await process.SendAsync(input);
        var result = await process.CompleteAsync();
        using var document = JsonDocument.Parse(input);
        var suppliedPassword = document.RootElement.GetProperty(PasswordProperty).GetString()!;
        Assert.False(result.Output.Contains(suppliedPassword, StringComparison.Ordinal), CredentialDisclosureFailure);
        return result;
    }
}
