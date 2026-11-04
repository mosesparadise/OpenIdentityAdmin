using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises actual browser lifecycle boundaries without simulating visibility or claiming bfcache eligibility.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "ApplicationLifecycle")]
public sealed class ApplicationLifecycleBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Real API-created output cannot survive native history restoration or an observed hidden-document transition.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_OneTimeApplicationOutput_When_HistoryOrVisibilityChanges_Then_SecretIsErasedWithoutPersistence(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(async () =>
        {
            var history = await ApplicationOutputLifecycle.RunAsync(fixture, provider, history: true);
            var visibility = await ApplicationOutputLifecycle.RunAsync(fixture, provider, history: false);
            return [.. history, .. visibility];
        });

    /// <summary>Each independently paused real TLS write is sent once and cannot publish after its original UI or authority boundary ends.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_PendingApplicationWrite_When_LifecycleSessionOrOriginalDeadlineEnds_Then_LateOutputCannotReturn(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(async () =>
        {
            List<string> secrets = [];
            foreach (var boundary in Enum.GetValues<ApplicationPendingBoundary>())
            {
                secrets.AddRange(await ApplicationPendingLifecycle.RunAsync(fixture, provider, boundary));
            }
            return secrets.ToArray();
        });
}
