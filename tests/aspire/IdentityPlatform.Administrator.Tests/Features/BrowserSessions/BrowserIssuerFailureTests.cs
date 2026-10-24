using System.Data.Common;
using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual provider failure/commit observations and withheld HTTP authority, with no automatic successful-ceremony retry.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerFailureTests(BootstrapFixture fixture)
{
    private const string SetCookie = "Set-Cookie";

    /// <summary>Real INSERT permission denial at admission or MFA rolls back all effects and emits no authenticated cookie or completion redirect.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuditWriteFailure_When_ActualBrowserPagesExecute_Then_NoPartialAdmissionOrSessionIsPublished(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var observation = new BrowserIssuerPermissionFailure();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider, observation);
        using var browser = host.Browser();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, IssuerContinuationProviderFixture.Selector());
        await DeniedAsync(scenario, observation, () => BrowserIssuerHttp.PasswordAsync(browser, scenario, entry));
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        await DeniedAsync(scenario, observation, () => BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form));
        using var accepted = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
    }

    /// <summary>An actual successful commit followed by lost acknowledgement leaves completion spent, with no cookie, selector or second grant on retry.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedMfaAcknowledgementLoss_When_ResponseFails_Then_DurableCompletionCannotBeRepublished(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var loss = new BrowserIssuerCommitLoss();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider, loss);
        using var browser = host.Browser();
        var intent = IssuerContinuationProviderFixture.Selector();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        loss.Armed = true;
        using var failed = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(1, loss.Commits);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.False(failed.Headers.Contains(SetCookie));
        Assert.Null(failed.Headers.Location);
        Assert.True((await BrowserIssuerJourneyTests.ContinuationAsync(scenario, intent)).State == IssuerContinuationPolicy.Completed);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var repeated = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        Assert.False(repeated.Headers.Contains(SetCookie));
        Assert.Null(repeated.Headers.Location);
        Assert.Equal(1, loss.Commits);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Restores permissions under independent cleanup and requires the exact native provider error to witness the denial path.</summary>
    private static async Task DeniedAsync(JourneyScenario scenario, BrowserIssuerPermissionFailure observation, Func<Task<HttpResponseMessage>> action)
    {
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var prior = observation.Failures;
        await IssuerAuditPermission.RunAsync(scenario.Lease, async () =>
        {
            using var denied = await action();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
            Assert.False(denied.Headers.Contains(SetCookie));
            Assert.Null(denied.Headers.Location);
        });
        Assert.True(observation.Failures > prior);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }
}

/// <summary>Observes actual provider permission errors without capturing command text, parameters or connection details.</summary>
internal sealed class BrowserIssuerPermissionFailure : DbCommandInterceptor
{
    private const string PostgreSqlPermission = "42501";
    private const int SqlServerPermission = 229;
    internal int Failures
    {
        get; private set;
    }

    /// <summary>Counts only the exact permission-denied category after a native command actually fails.</summary>
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Exception is Npgsql.PostgresException { SqlState: PostgreSqlPermission } or Microsoft.Data.SqlClient.SqlException { Number: SqlServerPermission })
        {
            Failures++;
        }
        return Task.CompletedTask;
    }
}

/// <summary>Injects loss after one actual armed commit, preserving durable state and preventing an unrelated exception from satisfying the witness.</summary>
internal sealed class BrowserIssuerCommitLoss : DbTransactionInterceptor
{
    private const string Lost = "Synthetic issuer browser commit acknowledgement loss.";
    internal bool Armed
    {
        get; set;
    }
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Waits for the native successful commit event before reporting a fixed safe failure to the HTTP owner.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            Armed = false;
            Commits++;
            throw new InvalidOperationException(Lost);
        }
        return Task.CompletedTask;
    }
}
