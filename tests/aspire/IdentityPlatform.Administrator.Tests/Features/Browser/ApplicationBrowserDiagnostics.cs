using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks actual generated values against the retained test diagnostic producer after owned browser/provider cleanup.</summary>
internal static class ApplicationBrowserDiagnostics
{
    private const int MaximumCharacters = 262144;

    /// <summary>Joins the secret-bearing scenario and cleanup before inspecting xUnit output and the genuine exception sanitizer.</summary>
    internal static Task RunAsync(Func<Task<string[]>> action) => AdministratorBrowserEvidence.RunAsync(async () =>
    {
        var secrets = await action();
        Assert.True(secrets.Length > 0 && secrets.All(value => !string.IsNullOrEmpty(value)));
        var failure = await Record.ExceptionAsync(() => AdministratorBrowserEvidence.RunAsync(() =>
            Task.FromException(new PlaywrightException(secrets[0]))));
        Assert.True(failure is Xunit.Sdk.XunitException);
        BrowserSecretOracle.RequireAbsent([failure!.ToString()], secrets);
        var context = TestContext.Current;
        Assert.True(context.TestOutputHelper is not null && context.Attachments is not null);
        Assert.Empty(context.Attachments!);
        var output = context.TestOutputHelper!.Output;
        Assert.True(output.Length <= MaximumCharacters);
        BrowserSecretOracle.RequireAbsent([output], secrets);
    });
}
